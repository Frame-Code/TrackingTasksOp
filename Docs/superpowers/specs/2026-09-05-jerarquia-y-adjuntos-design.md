# Jerarquía de work packages y adjuntos — Diseño

**Fecha:** 2026-09-05
**Estado:** Aprobado — documentado, **sin implementar**

## 1. Contexto y problema

Dos necesidades que llegaron juntas y comparten una misma decisión de fondo: **OpenProject ya tiene los datos, la app solo tiene que exponerlos bien**.

1. **Jerarquía.** OpenProject modela relaciones padre/hijo entre work packages, y hoy la app las ignora por completo: `WorkPackageLinks` (`Domain/Entities/OpenProjectEntities/WorkPackage/WorkPackageLinks.cs`) mapea `status`, `type`, `priority`, `assignee`, `responsible` y `project`, y nada más. No hay forma de ver de qué cuelga una tarea ni de crear una subtarea.

2. **Adjuntos.** La gente de oficina y los implementadores **ya adjuntan fotos en OpenProject** (confirmado con el usuario: ese es el flujo actual, no hay que inventarlo). Los devs necesitan verlas desde la app, junto con la descripción de la tarea. Y al crear una tarea por el bot, poder mandar las fotos en el mismo acto.

El punto de partida arquitectónico, del que se deriva casi todo lo demás:

> **La app no almacena ni un byte de esos archivos.** OpenProject es el dueño: ya los respalda, ya resuelve los permisos, ya los versiona. Cualquier copia propia sería un segundo lugar donde la foto puede faltar, envejecer o filtrarse.

Hay un precedente explícito en el repo que apunta en la misma dirección: `UpdateAvatarCommandImpl` guarda el avatar en Postgres con un tope de 512 KB y el comentario *"no está para ahorrar espacio, sino para que nadie use la columna como depósito de archivos"*.

## 2. Alcance

**Incluye:**

- Mapeo de `parent`, `children` y `ancestors` en el dominio.
- Una **vista Árbol** nueva, hermana de la grilla actual, con carga perezosa por nivel y el árbol completo (hijos asignados a cualquier persona).
- Una **miga de pan** (`#412 > #418 > .`) en la card de la grilla.
- Crear subtareas desde el bot, vía un parámetro `parentId` en la acción `create_task` existente.
- Un modal **"Ver más detalles"** de solo lectura: descripción + galería de adjuntos + descarga.
- Un **proxy** autenticado para servir los adjuntos al navegador.
- Subir fotos **al crear una tarea desde el bot**, con garantía de no reportar falsos positivos.

**No incluye (fuera de alcance de este spec):**

- **Video**: decisión explícita del usuario. Los videos adjuntos aparecen listados y se pueden descargar, pero no se reproducen embebidos. Eso evita tener que reenviar cabeceras `Range` / devolver `206`, y evita tocar `attachment_max_size` en la instancia de OpenProject.
- **Subir adjuntos desde el modal**: la subida ocurre únicamente en el flujo de creación del bot.
- **Editar o borrar adjuntos** desde la app: es trabajo de OpenProject; duplicarlo trae problemas de permisos sin beneficio.
- **Miniaturas generadas por nosotros** (ImageSharp/SkiaSharp, tabla de thumbnails): se agrega cuando una tarea con ~20 fotos se sienta lenta, no antes.
- **Caché de archivos en disco o Postgres**: la caché del navegador ya lo resuelve (ver §7.3).
- **Autorización propia sobre adjuntos**: OpenProject ya la aplica (ver §7.3).

## 3. Paso 0 — Spike bloqueante

Tres incógnitas siguen sin verificar contra la instancia real. **Dos de ellas cambian el diseño**, así que se resuelven ANTES de escribir código. Se contestan con tres `curl` autenticados contra el OpenProject de test.

| # | Pregunta | Si la respuesta es NO |
|---|---|---|
| 1 | ¿`_links.ancestors` viene en la respuesta de **colección** de `/api/v3/work_packages`? | La miga de pan deja de ser gratis. Se degrada a mostrar solo el **padre directo** (`_links.parent`, que sí viene seguro), sin la cadena completa. |
| 2 | ¿La API v3 acepta adjuntos **sin contenedor** (`POST /api/v3/attachments`) para enlazarlos después? | Se queda la opción A de §8 (el navegador re-envía las fotos por turno). Si es SÍ, habilita la opción B como mejora posterior que no rehace el handler. |
| 3 | ¿OpenProject 15 expone **miniaturas** por API v3? | La galería del modal usa las imágenes completas escaladas por CSS. Si es SÍ, se usan las miniaturas y la galería es más rápida gratis. |

**Bloqueo conocido:** el spike necesita una API key que funcione, y hoy el registro en la instancia de test devuelve **401** (`InvalidApiKeyException`, logs del 2026-09-05 20:20). Ese 401 hay que resolverlo primero — es prerrequisito del spike, y el spike es prerrequisito del plan.

## 4. Requerimiento 1 — Jerarquía

### 4.1. Dominio

`Domain/Entities/OpenProjectEntities/WorkPackage/WorkPackageLinks.cs` suma tres campos:

```csharp
[JsonPropertyName("parent")]
public LinkObject Parent { get; set; } = new LinkObject();

[JsonPropertyName("children")]
public List<LinkObject> Children { get; set; } = new();

[JsonPropertyName("ancestors")]
public List<LinkObject> Ancestors { get; set; } = new();
```

`LinkObject` ya trae `href` + `title`, que es todo lo que necesita la miga de pan: el título del ancestro viene en el mismo payload, sin resolver nada.

### 4.2. Carga perezosa, un nivel por expansión

Traer el árbol completo de una es repetir un error ya documentado en `ListsWorkPackagesCommandImpl`: OpenProject cobra ~30 ms por work package serializado, y la lección que quedó escrita ahí es *"hay que pedir MENOS, no a la vez"*.

```
CARGA INICIAL (0 llamadas extra)
  mis tareas ya vienen con ancestors:
     #432 -> ancestors: [#412, #418]
     #451 -> ancestors: [#412]
     #503 -> ancestors: []            <- huerfana, es raiz de si misma
  raices distintas: #412, #503        <- se deduce en el navegador

AL EXPANDIR UN NODO (1 llamada, una sola vez)
  GET /api/v1/workpackage/412/children
    -> filters=[{"parent":{"operator":"=","values":["412"]}}]
    <- hijos SIN filtro de assignee
  y queda cacheado en memoria: colapsar y reabrir no repite la llamada
```

Las raíces **no necesitan endpoint**: salen de datos que el navegador ya tiene.

### 4.3. Puerto nuevo

`Application/Ports/UseCases/WorkPackages/IGetWorkPackageChildrenQuery.cs`:

```csharp
public interface IGetWorkPackageChildrenQuery
{
    Task<List<WorkPackage>> ExecuteAsync(int parentId, CancellationToken ct = default);
}
```

### 4.4. Implementación

`Infrastructure/Adapters/UseCases/WorkPackages/GetWorkPackageChildrenQueryImpl.cs`, siguiendo el patrón de `ListsWorkPackagesCommandImpl` (mismo `HttpClient` nombrado, misma deserialización a `WorkPackageCollection`).

**La diferencia crítica con el listado actual:** este filtro **no lleva `assignee = me`**. Es deliberado y es el corazón del requerimiento — el árbol muestra hijos de cualquier persona.

**Autorización:** no se escribe ninguna. La llamada sale con la API key del usuario vía `OpenProjectAuthHandler`, así que OpenProject devuelve únicamente lo que esa persona puede ver. "Árbol completo" significa **completo dentro de los permisos del usuario**.

### 4.5. Controlador

`Web/Controllers/WorkPackageController.cs`, siguiendo la convención existente (`/api/v1/workpackage/...`):

```csharp
[HttpGet("{id:int}/children")]
public async Task<ActionResult<List<WorkPackage>>> GetChildren(int id, CancellationToken ct)
    => await childrenQuery.ExecuteAsync(id, ct);
```

### 4.6. Frontend — la vista Árbol

Es la pieza de UI más grande del spec. Vive en un archivo nuevo, `Web/wwwroot/js/tree.js`, para no seguir engordando `render.js` (29 KB) ni `app.js` (43 KB).

```
  Tareas          [ Grilla ]  [ *Arbol* ]        [buscar...]
  ----------------------------------------------------------

  v #412  Implantacion Cliente Norte             ###...   60%
    |
    +-v #418  Levantamiento de datos             ######  100%
    |   +-- #431 Formularios          ( ) Ana    ###.     75%
    |   +-- #433 Acta de firma        ( ) Luis   ....      0%
    |   +-- #432 Fotos de sitio       (o) VOS    #...     20%  [>]
    |
    +-> #419  Migracion  (3 hijos)    ( ) Luis   #...     15%

  > #501  Soporte Mensual (8 hijos)              ##..     40%

  ----------------------------------------------------------
  (o) asignada a vos: accionable    ( ) de otra persona
  [>] iniciar sesion                v / >  expandir / colapsar
```

**Por qué una vista aparte y no cards expandibles.** Son dos trabajos distintos:

| | Grilla | Árbol |
|---|---|---|
| Pregunta que responde | *"¿qué hago ahora?"* | *"¿cómo va la implantación del Cliente Norte?"* |
| Alcance | mis tareas | todo, sea de quien sea |
| Verbos | iniciar, pausar, finalizar | expandir, ubicar, crear subtarea |
| Ritmo | varias veces al día, rápido | ocasional, con detenimiento |

Meter el árbol dentro de la grilla obligaría a una superficie con verbos de acción a cargar tareas que **no se pueden accionar**, con cards de dos alturas posibles y saltos de layout al expandir.

**Distinción visual obligatoria:** las tareas propias ofrecen `[>] Iniciar`; las ajenas se ven pero no ofrecen el botón. Esa asimetría es la razón de ser de la vista.

**Estado:** `state.js` suma el modo de vista actual (`grilla` | `arbol`) y un caché `childrenByParentId` para que colapsar/expandir no repita llamadas.

**El botón `[ + Crear subtarea aquí ]`** de cada nodo llama al **mismo** camino de creación que el bot (§5), con el `parentId` del nodo. No se reimplementa la creación.

### 4.7. Frontend — la miga de pan en la card

En `buildCard` (`render.js`), un renglón gris entre el eyebrow y el título:

```
  +------------------------------------+
  | TAREA  #432                 [Nuevo]|
  | #412 > #418 > .              <---- miga, un renglon, gris
  | Fotos de sitio                     |
  | [f] Cliente Norte                  |
  | ##......    20%                    |
  | [t][h][^]        [> Iniciar]       |
  +------------------------------------+
                ^
                +-- "ver en el arbol": abre la vista Arbol ya expandida
                    y con esta tarea marcada
```

Sale de `_links.ancestors`, que ya viene en el payload — **cero llamadas extra**, sujeto a la incógnita 1 del §3.

**Un concepto, una casa.** La jerarquía vive en la vista Árbol. En los demás lugares hay *señal y enlace*, nunca una copia: por eso el modal de detalles (§6) **no** tiene pestaña de jerarquía, solo un enlace `[ Ver en el árbol ]`. Tres implementaciones del mismo árbol divergirían a tres velocidades distintas.

## 5. Requerimiento 1b — Crear subtareas desde el bot

### 5.1. No se agrega una acción nueva

`create_subtask` sería un segundo camino que hace lo mismo que `create_task`, divergiendo con el tiempo en validaciones, defaults y mensajes de error. **Una subtarea es una tarea que sabe de quién cuelga**: `create_task` gana un parámetro `parentId`.

```
  GroqTools.cs            +  ["parentId"] en el schema de create_task
        |
  StartTaskRequestBuilder +  resuelve el padre (lo comparten create_task y start_task)
        |
  CreateWorkPackageRequest+  ParentId
        |
  CreateWorkPackageCommandImpl
        +--> links["parent"] = { href = "/api/v3/work_packages/412" }
             (el JsonObject de _links ya esta armado ahi: es UNA linea)
```

### 5.2. `parentId` es numérico, y es la excepción correcta

La regla del system prompt dice *"nunca un ID"* para proyecto, estado y asignado — correcto, nadie dice "asigname el usuario 47". El padre es la excepción legítima: la gente **sí** dice *"creá una subtarea dentro de la #412"*. Y ya hay precedente: `start_task` acepta `workPackageId` numérico.

Tres formas de nombrar al padre, ninguna requiere infraestructura nueva:

```
  "crea una subtarea dentro de la #412 llamada Acta de firma"
      -> parentId: 412, directo.

  "crea una subtarea de Levantamiento de datos"
      -> el resolver busca por asunto. Una sola coincidencia -> la usa.
         Varias -> el bot pregunta cual (mismo patron que ya se usa
         cuando falta un campo personalizado).

  "...y crea una tarea hija aca"  (tras listar o crear otra)
      -> contextWpId. Ya existe: BotActionExecutor lo saca del "ID:"
         de una accion previa en la misma respuesta. Cero codigo nuevo.
```

### 5.3. El proyecto se deduce del padre

Hoy `create_task` exige `projectName`. Si viene `parentId`, el proyecto sale del padre — una subtarea vive donde vive su padre. Eso hace que *"creá una subtarea dentro de la #412 llamada Acta de firma"* sea un mensaje suficiente, sin repreguntar. No cuesta una llamada extra: hay que traer el padre igual, para validar que existe y que el usuario lo puede ver.

### 5.4. Errores

OpenProject rechaza ciertas combinaciones padre/hijo (restricciones por tipo, y jerarquías entre proyectos según la configuración de la instancia) con **422** y un mensaje concreto. Ese mensaje **se propaga**, no se traga: mismo criterio que la regla 9 del system prompt, que ante una transición de estado inválida devuelve los estados intermedios válidos en vez de un error genérico.

## 6. Requerimiento 2 — Modal de detalles (solo lectura)

```
  +- #432 - Fotos de sitio ------------------------------ X -+
  |  TAREA    #412 > #418 > #432          [f] Cliente Norte  |
  |                                                          |
  |  DESCRIPCION                                             |
  |  Revisar el tablero del piso 3. El cliente reporta que   |
  |  el breaker salta con carga. Adjunto fotos del gabinete  |
  |  abierto y el acta firmada.                              |
  |                                                          |
  |  ADJUNTOS (4)                                            |
  |  +---------+ +---------+ +---------+ +----------------+  |
  |  |  [img]  | |  [img]  | |  [img]  | |     [PDF]      |  |
  |  |         | |         | |         | | acta.pdf 240KB |  |
  |  +---------+ +---------+ +---------+ +----------------+  |
  |   IMG_01.jpg   IMG_02.jpg  IMG_03.jpg   [ descargar ]    |
  |   Ana - ayer                                             |
  |                                                          |
  |                                    [ Ver en el arbol ]   |
  +----------------------------------------------------------+
         click en una foto -> lightbox a pantalla completa
```

**No hay botón de subir.** La subida ocurre en el bot (§8).

Se abre desde un botón nuevo en la card, junto a los accesorios existentes (`btn-log-time`, `btn-history`), y reusa el patrón del modal de historial que ya existe.

## 7. Requerimiento 2 — Backend de adjuntos

### 7.1. Dominio

`Domain/Entities/OpenProjectEntities/Attachment/Attachment.cs` y `AttachmentCollection.cs`, con la forma de la respuesta de OpenProject: `id`, `fileName`, `fileSize`, `contentType`, `createdAt`, `_links.author`.

`FormattableField` ya mapea `raw`, que es lo que hace falta para la descripción. No se toca.

### 7.2. Un solo viaje para abrir el modal

`GET /api/v1/workpackage/{id}/details` devuelve descripción + ancestors + lista de adjuntos. Por dentro son dos llamadas a OpenProject **en paralelo** (`Task.WhenAll`, mismo patrón que ya usa `ListsWorkPackagesCommandImpl` para las páginas). Abrir el modal es **una** petición del navegador, no cinco.

**Degradación:** si la lista de adjuntos falla, el modal abre igual con la descripción. Son dos llamadas independientes y el detalle es útil con una sola.

### 7.3. El proxy de contenido

`GET /api/v1/attachments/{id}/content` — deliberadamente **no** colgado de la ruta del work package.

**Por qué no hace falta validar que el adjunto pertenece a la tarea:** la llamada sale con la API key del usuario, y **OpenProject aplica sus propios permisos**. Si pide un adjunto que no le corresponde, OpenProject responde 403 y nosotros lo repetimos. Reimplementar esa autorización de este lado sería una segunda fuente de verdad que puede desincronizarse de la de OpenProject.

**Por qué hace falta un proxy y no un `<img src>` directo:** la API key vive cifrada en la base (Data Protection) y se descifra en el servidor dentro de `OpenProjectAuthHandler`. Mandarla al navegador para que arme el `src` sería regalar la credencial de OpenProject del usuario a cualquier script de la página. Además, en el ambiente de test la app llega a OpenProject por una URL interna (`http://openproject`) que el navegador no resuelve.

**Seguridad — no negociable.** El proxy sirve archivos subidos por terceros **desde nuestro propio origen**: es un vector de XSS almacenado de manual (alguien adjunta un `.html`, o un SVG con `<script>`, un dev abre el enlace y el script corre con la sesión de la app).

```
  Content-Type de OpenProject
        |
        +-- image/jpeg, image/png, image/gif, image/webp
        |      -> inline, se muestra en la galeria
        |
        +-- cualquier otra cosa (incluido image/svg+xml)
               -> Content-Disposition: attachment  (se descarga, no se abre)

  Siempre, en las dos ramas:
        X-Content-Type-Options: nosniff
        Cache-Control: private, immutable, max-age=31536000
        ETag: "<id del adjunto>"
```

`image/svg+xml` queda del lado de "descargar" **a propósito** aunque sea una imagen: es XML y ejecuta scripts.

**Por qué `immutable` es honesto acá:** el contenido de un adjunto en OpenProject no cambia — el id `9871` apunta siempre al mismo archivo, y editar una foto crea un adjunto nuevo con otro id. El navegador descarga cada foto **una sola vez en la vida**; abrir el mismo detalle diez veces no le pega a OpenProject ni una. **Ésta es la razón por la que no hacen falta miniaturas, ni caché en disco, ni caché en Postgres.**

**Streaming:** el contenido se reenvía sin bufferear el archivo completo en memoria ni tocar disco.

### 7.4. Frontend — descripción en Markdown

La descripción viene en Markdown, y hoy el front no tiene ni `marked` ni `DOMPurify`: todo pasa por `escHtml`. Inyectar el HTML que devuelve OpenProject sería meter HTML de terceros dentro de la sesión del usuario.

**Solución:** un render de subconjunto de ~30 líneas en `helpers.js` que **escapa primero todo** y recién después aplica una lista blanca de patrones (negrita, itálica, listas, saltos de línea, enlaces). Seguro por construcción, sin dependencias nuevas, y suficiente para lo que escribe la gente de oficina.

## 8. Requerimiento 2b — Subir fotos al crear una tarea por el bot

### 8.1. El problema

En OpenProject un adjunto necesita un contenedor, y cuando el usuario pega la foto en el chat la tarea **todavía no existe**. Peor: la regla 3 del system prompt hace que el bot confirme antes de crear, así que pasan varios turnos entre el clip y el `create_task`:

```
  turno 1   usuario: [clip x3] "crea una tarea Fotos de sitio dentro de la #418"
  turno 2   bot:     "no indicaste fecha de inicio, usare hoy. procedo?"
  turno 3   usuario: "dale"
  turno 4   bot:     -> create_task  ->  recien ACA existe el WP 432
```

### 8.2. Requisito duro: sin falsos positivos

**El bot solo dice "tarea creada con éxito" si toda la data llegó, fotos incluidas.** Hay dos fuentes distintas de falso positivo y las dos hay que cerrarlas:

```
  (1) ORDEN:  el mensaje de exito se emite antes de que las fotos lleguen
  (2) EL LLM: Groq narra por su cuenta "listo, ya cree la tarea!" aunque
              el handler haya devuelto un error
```

Para **(2)**: la regla 7 del system prompt ya prohíbe inventar resultados, pero **solo para las acciones de lectura**. Hay que extender esa prohibición a `create_task`: el handler es el único autorizado a declarar el resultado.

Para **(1)**: la consecuencia es estructural — **la foto tiene que estar del lado del servidor cuando `create_task` se ejecuta**. Si no, no hay forma honesta de emitir un único mensaje de éxito.

### 8.3. Opción elegida (A): las fotos viajan con el mensaje del chat

El navegador retiene las fotos y las **re-envía en cada turno** mientras estén pendientes. El endpoint de chat pasa a `multipart`, igual que ya hace el de voz. Cuando `create_task` dispara, las fotos están en ese mismo request.

```
  handler create_task, en orden estricto:

    1. crea el WP en OpenProject            -> id 432
    2. sube cada foto a /432/attachments
    3. recien ahora compone el mensaje:

       3 de 3 -> "Tarea Fotos de sitio creada (ID: 432) con 3 fotos adjuntas."
       2 de 3 -> "Tarea creada (ID: 432), pero 1 de 3 fotos no se adjunto:
                  IMG_03.jpg supera el limite. Puedo reintentar."
       0 de 3 -> "Tarea creada (ID: 432) SIN fotos: <motivo>."
```

Costo: re-enviar las fotos durante 1–3 turnos, que con el reescalado del navegador son ~300 KB cada una y van contra nuestro propio servidor. **Cero almacenamiento temporal, cero TTL, cero limpieza.**

**Alternativas descartadas:**

- **Redis o tabla temporal con TTL** — introduce el almacenamiento propio que toda esta arquitectura evita: limpieza, respaldo y una segunda copia de la foto. **No.**
- **Subir después de crear y componer el mensaje en el navegador** — parte la fuente de verdad del mensaje de éxito en dos lugares. Es exactamente donde se cuelan los falsos positivos. **No.**
- **Adjuntos sin contenedor en OpenProject (opción B)** — mejor en todo sentido: no se re-envía nada y la foto está a salvo desde el clip; solo viajan ids por el contexto de conversación. Depende de la incógnita 2 del §3. Si el spike la confirma, es una mejora posterior que **no rehace el handler**: cambia de dónde salen los bytes, no el orden de las operaciones ni el mensaje.

### 8.4. Sin rollback ante éxito parcial

Si dos fotos suben y una falla, **no se borra la tarea** para simular atomicidad. Borrar en OpenProject algo que el usuario pidió crear es peor que un éxito parcial bien reportado — y el reporte parcial del §8.3 no es un falso positivo: es la verdad, con una salida ofrecida.

### 8.5. Cambios necesarios

```
  bot.html          + boton de clip junto al microfono con previews
                      (hoy no hay ningun input type=file; el mic con
                      FormData ya es el precedente)

  BotController     + el chat devuelve hoy solo { response }. El id de la
                      tarea creada viaja DENTRO del texto ("ID: 432").
                      Parsear texto desde el navegador es fragil:
                      se agrega createdWorkPackageId al JSON.
                      BotActionExecutor ya conoce ese id.
                    + el endpoint pasa a aceptar multipart

  CreateTaskActionHandler
                    + orden estricto crear -> adjuntar -> componer mensaje

  IAttachmentService+ subida multipart de DOS partes contra OpenProject
                      (ver 8.6)

  GroqApiClient     + regla nueva: prohibido narrar el resultado de
                      create_task
```

### 8.6. Dos trampas conocidas de antemano

**El multipart de OpenProject no es el habitual.** No acepta un multipart simple con el archivo: quiere **dos partes**, una `metadata` con un JSON que lleva el `fileName`, y otra `file` con el binario. Es la clase de detalle que cuesta media tarde si se descubre depurando.

**Las fotos de celular pesan 3–8 MB y OpenProject topa los adjuntos en ~5 MB por defecto.** Se reescalan en el navegador antes de subir. **Ese código ya existe en el repo**: `settings.js` líneas 96–118 hace exactamente eso para el avatar (`FileReader` → `canvas` → `drawImage` → `toDataURL('image/jpeg', calidad)`). Se extrae a `helpers.js` con el tamaño como parámetro y se usa en los dos lados.

## 9. Orden de implementación

De riesgo creciente, dejando algo usable en cada etapa:

| # | Etapa | Por qué en este lugar |
|---|---|---|
| 0 | **Spike** (§3) | Dos respuestas cambian el diseño. Bloqueado por el 401 del registro. |
| 1 | **Subtarea por bot** (§5) | Lo más barato, backend puro, sin incógnitas. |
| 2 | **Jerarquía: dominio + endpoint + vista Árbol + miga** (§4) | La UI más grande, pero de riesgo acotado. Aprovecha el `parentId` de la etapa 1 para el botón "crear subtarea aquí". |
| 3 | **Modal + lectura de adjuntos** (§6, §7) | Introduce el proxy, que es donde está el riesgo de seguridad. |
| 4 | **Subida por el bot** (§8) | Lo más riesgoso: cambia el contrato del endpoint de chat, tiene el requisito de no-falsos-positivos y el multipart raro. Se hace último, con el terreno ya conocido. |

## 10. Costo

**De construcción:**

| Pieza | Backend | Frontend | Riesgo |
|---|---|---|---|
| Árbol (jerarquía + carga perezosa) | `WorkPackageLinks` += 3 campos · 1 puerto + 1 impl · 1 endpoint | Vista nueva: nodo recursivo, estado de expansión, caché, CSS | Bajo atrás, **medio adelante** |
| Miga de pan | — (sale del mismo payload) | ~10 líneas en `buildCard` | Bajo · depende de la incógnita 1 |
| Subtarea por bot | `parentId` en el schema · resolución del padre · `_links.parent` · 422 con motivo real | El botón del árbol reusa todo | **Bajo — lo más barato** |
| Modal + ver adjuntos | 2 entidades · 1 puerto + 1 impl · endpoint de detalles · proxy | Modal, galería, lightbox, markdown seguro | Medio — el riesgo es de **seguridad** |
| Subida por el bot (A) | Chat a `multipart` · orden estricto · multipart de dos partes · regla del prompt | Clip + previews · reescalado extraído de `settings.js` | **El más alto** |

**De operación: prácticamente cero.** Es consecuencia directa de la decisión del §1:

```
  cero almacenamiento nuevo      cero dependencias nuevas
  cero respaldo nuevo            cero retencion que administrar
  cero permisos propios          cero procesamiento de imagen en el server

  trafico:  cada foto se baja UNA VEZ por navegador (immutable)
  carga en OpenProject:  +1 llamada por nodo expandido
                         +2 llamadas al abrir un modal
```

Lo que **no** se construye es la razón de que el costo sea este y no el triple: storage, backup, miniaturas, caché de servidor, autorización propia, y —desde que el video quedó fuera— streaming con `Range`.

## 11. Testing

Siguiendo lo que ya existe en `Tests/`:

| Qué | Dónde | Por qué es el test que importa |
|---|---|---|
| El filtro de hijos **no** incluye `assignee` | `Tests/Infrastructure/.../GetWorkPackageChildrenQueryTests` | Es el corazón del requerimiento "árbol completo". Si alguien reusa el builder del listado por comodidad, esto se rompe en silencio. |
| `ParentId` emite `_links.parent` | `Tests/Infrastructure/.../CreateWorkPackageCommandTests` | Una línea fácil de perder en un merge. |
| Composición del mensaje: 3/3, 2/3, 0/3 fotos | `Tests/Infrastructure/.../CreateTaskActionHandlerTests` | **El test del requisito de no-falsos-positivos.** Es el más importante del spec. |
| Cabeceras del proxy: allowlist, `nosniff`, `Content-Disposition`, caché | `Tests/Web/.../AttachmentProxyTests` | Lógica de seguridad pura, sin I/O: barata de testear y cara de equivocar. Incluye el caso SVG. |
| Render de markdown: que `<script>` salga escapado | `Tests/` o self-check en `helpers.js` | Es la otra mitad de la superficie XSS. |

## 12. Decisiones ya validadas con el usuario (no re-abrir)

1. **Las fotos ya se adjuntan en OpenProject hoy.** No hay que construir captura ni almacenamiento.
2. **El árbol muestra el árbol completo**, con hijos asignados a cualquier persona — no solo los propios.
3. **El árbol es una vista aparte** (toggle Grilla | Árbol), no cards expandibles ni una pestaña del modal. Más una miga de pan en la card.
4. **El modal NO tiene pestaña de jerarquía**, solo un enlace "Ver en el árbol". Un concepto, una casa.
5. **Video fuera de alcance.** Se lista y se descarga; no se reproduce embebido.
6. **La subida de fotos ocurre en el bot al crear la tarea**, no en el modal. El modal es de solo lectura.
7. **Sin falsos positivos:** el mensaje de éxito se emite después de los adjuntos y solo por el handler.
8. **Opción A** para las fotos pendientes (viajan con el mensaje del chat). Opción B queda como mejora si el spike la habilita.
