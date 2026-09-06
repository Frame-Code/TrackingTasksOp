# Ambiente de test en el server

Segundo stack completo (app + Postgres + Redis + **OpenProject vacío**) corriendo en el mismo
server que producción, aislado por nombre de proyecto Compose. Sirve para probar contra datos
desechables — crear work packages, romper cosas, borrar la base — sin tocar la instancia real
de OpenProject ni la base de los 5 usuarios.

Lo que separa test de producción es el **nombre de proyecto Compose**: cambia contenedores, red
y **volúmenes**. Ese nombre no se pasa por línea de comandos sino que vive en el `.env` del clon
de test (`COMPOSE_PROJECT_NAME` + `COMPOSE_FILE`), así que **cada directorio manda sobre su
propio stack** y un `docker compose up -d` pelado hace lo correcto en los dos.

> No volver a los flags `-p` / `-f` / `--env-file`. Cuando el aislamiento depende de que alguien
> los escriba, tarde o temprano no se escriben: un `docker compose up -d` sin ellos desde el clon
> de test recrea los contenedores de **producción** con todas las variables en blanco. Pasó el
> 2026-09-05.

## 0. Antes de empezar: RAM

La imagen all-in-one de OpenProject pide ~2 GB para sí sola, y el stack de producción ya está
corriendo. Verificar:

```bash
free -h
```

Con menos de ~6 GB libres el stack de test va a competir con producción (o el OOM killer va a
matar Postgres). Alternativa si no alcanza: levantar test **solo cuando se use** y bajarlo al
terminar (paso 6).

## 1. Clonar aparte

Un clon distinto del de producción, para poder tener test en otra rama sin tocar el deploy vivo:

```bash
cd <directorio-del-deploy>
git clone <url-del-repo> test
cd test
git checkout test_AI_Stin
```

## 2. `.env`

Se llama `.env` a secas, **dentro del clon de test**. Compose lo lee solo, y de ahí saca el
nombre de proyecto y los archivos a combinar:

```bash
cp .env.test.example .env
chmod 600 .env
nano .env    # OP_SECRET_KEY_BASE y GROQ_API_KEY
```

`OP_SECRET_KEY_BASE`: `openssl rand -hex 32`.
`GROQ_API_KEY`: **obligatoria** — la misma de producción. Sin ella el arranque muere con
`Groq:ApiKey is not set` y el contenedor queda en `Restarting`.

Los puertos ya vienen corridos (`5001` app, `5433` Postgres, `8081` OpenProject) para no chocar
con los de producción.

## 3. Levantar

Desde el clon de test, sin flags:

```bash
docker compose up -d --build
```

Verificar que agarró el proyecto correcto antes de seguir — todo debe decir
`trackingtasksop-test-*`:

```bash
docker compose ps
```

El primer arranque de OpenProject corre sus migraciones y **tarda varios minutos**. Seguirlo con:

```bash
docker compose logs -f openproject
```

Está listo cuando responde:

```bash
curl -I http://127.0.0.1:8081
```

## 4. Configurar OpenProject vacío

Túnel desde la máquina local (o publicarlo en el tailnet, paso 5b):

```bash
ssh -L 8081:localhost:8081 <usuario>@<server>
```

Y en el navegador `http://localhost:8081`:

1. Login inicial: `admin` / `admin`, obliga a cambiar la contraseña.
2. Crear un proyecto y un par de work packages asignados al admin.
3. Avatar → *Mi cuenta* → *Access tokens* → generar una **API key**. Se muestra una sola vez.

## 5. Conectar la app de test

Túnel a la app y registrarse en ella:

```bash
ssh -L 5001:localhost:5001 <usuario>@<server>
```

Al registrar el usuario, la **URL de la instancia de OpenProject es la misma URL pública que usa
el navegador** (`https://<host-del-tailnet>:8082`), no el nombre interno `openproject`. Pegar
también la API key del paso 4.

> Cuesta creerlo estando los dos contenedores en la misma red, pero el nombre interno **no
> sirve**: el filtro anti-SSRF de la propia app (`SsrfSafeHttpHandler`) bloquea `172.16/12`,
> que es donde vive la red de Compose, y el registro falla con *"No se pudo conectar a
> OpenProject"*. Fuera de `Development` ese filtro es deliberado — la URL de instancia la
> declara el usuario — así que el ambiente de test entra por la URL pública: TLS real, IP del
> tailnet (`100.64/10`, que el filtro sí permite) y el host que OpenProject espera. Por eso el
> compose declara `extra_hosts` en el servicio `app`: el contenedor no resuelve MagicDNS.

> Dos cosas más que hacen fallar el registro, ambas ya cubiertas en el compose y el `.env`:
> desde OpenProject 14.3 se valida el header `Host` y se responde **400** a todo lo que no
> coincida (de ahí `OPENPROJECT_ADDITIONAL__HOST__NAMES`), y con `OP_HTTPS=true` OpenProject
> redirige `http`→`https` sobre el mismo host, lo que deja colgada cualquier llamada interna.

> La cookie de sesión es `Secure` (`CookieSettings__UseSecurePolicy: true`), así que por
> `http://localhost:5001` el navegador **sí** la manda: `localhost` cuenta como origen seguro.
> Por una IP o un dominio en HTTP plano, no. Si se quiere acceso sin túnel, publicarlo en el
> tailnet (paso 5b), que termina TLS.

## 5b. Publicar en el tailnet (opcional, en vez de túneles SSH)

`tailscale serve` acepta cualquier puerto (la restricción a 443 / 8443 / 10000 es de **Funnel**,
que es otra cosa: publica en internet abierto, no en el tailnet). Mirar primero qué hay ocupado,
que en esta máquina no es solo lo de esta app:

```bash
tailscale serve status
```

Y usar el mismo número que el puerto local, para no recordar equivalencias:

```bash
tailscale serve --bg --https=5001 http://127.0.0.1:5001    # app de test
tailscale serve --bg --https=8081 http://127.0.0.1:8081    # OpenProject de test
```

La app anda tal cual. **OpenProject no**: rechaza cualquier host que no sea su
`OPENPROJECT_HOST__NAME`, y con `OPENPROJECT_HTTPS=false` genera links `http://` detrás del TLS
que termina Tailscale. Descomentar en el `.env` del clon de test y recrear el contenedor
(`docker compose up -d openproject`):

```bash
OP_HOST_NAME=<host-del-tailnet>:10000
OP_HTTPS=true
```

Para dejar de publicarlos: `tailscale serve --https=5001 off` (ídem el de OpenProject).

## 6. Ciclo de trabajo

Todo desde el clon de test. **Comprobar con `pwd` antes de cualquier `down`**: el mismo comando
en el clon de producción hace lo mismo con la base de los usuarios.

```bash
# Actualizar test con lo último de la rama
cd <directorio-del-deploy>/test && git pull
docker compose up -d --build

# Apagar test sin borrar nada (deja libre la RAM)
docker compose stop

# Empezar de cero: borra la base de la app Y la de OpenProject
docker compose down -v
```

El `-v` borra los volúmenes `trackingtasksop-test_*`. Los de producción (`trackingtasksop_*`) no
se tocan, pero conviene mirar `docker volume ls` antes de correrlo.

## 7. Apagado nocturno

El cron que hace `docker compose stop` antes del `shutdown` solo conoce el directorio de
producción. Agregar el de test para que su Postgres también cierre limpio:

```bash
cd <directorio-del-deploy>/test && docker compose stop
```
