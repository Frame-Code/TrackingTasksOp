# Data Protection (cifrado de API keys de OpenProject)

## ¿Para qué se usa?

Cuando un usuario se registra/inicia sesión local, su **API key de OpenProject** se
guarda cifrada en la tabla `LocalCredentials.EncryptedApiKey` mediante la
[Data Protection API de ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/data-protection/introduction).

El cifrado/descifrado lo hace `DataProtectionApiKeyEncryptorImpl`
(`Infrastructure/Adapters/Services/DataProtectionApiKeyEncryptorImpl.cs`), que obtiene
un `IDataProtector` a partir del *key ring* configurado en
`AddTrackingDataProtection` (`Infrastructure/Extensions/DataProtectionExtensions.cs`).

## Dónde se guardan las claves (key ring)

En la **tabla `DataProtectionKeys` de la propia base de datos**, vía
`PersistKeysToDbContext<TrackingTasksDbContext>()`. El `DbContext` implementa
`IDataProtectionKeyContext` y expone el `DbSet<DataProtectionKey>`.

**No requiere configuración.** La tabla la crea la migración `DataProtectionKeysToDb`, y
ASP.NET Core genera la primera clave sola cuando se necesita cifrar algo.

```jsonc
// Web/appsettings.json
"DataProtectionSettings": {
  "ApplicationName": "TrackingTaskOp",
  "KeyRingCertificatePath": "",      // opcional, ver abajo
  "KeyRingCertificatePassword": ""
}
```

### Por qué en la base y no en disco

Esto es lo importante y la razón del cambio. El cifrado de las API keys es *envelope
encryption*: hay **dos mitades** —el dato cifrado y la clave que lo descifra— y ambas
tienen que existir juntas para que sirvan de algo.

Antes el key ring vivía en disco (`Web/Keys/`, montado en el volumen Docker `keysdata`) y
el dato cifrado en Postgres. Eran **dos artefactos que había que respaldar juntos**, y
nada lo garantizaba: un `pg_dump` capturaba una mitad y dejaba la otra atrás. Cuando ese
dump se restauraba, las API keys quedaban indescifrables:

```
System.Security.Cryptography.CryptographicException: The key {xxxxxxxx-...} was not found in the key ring.
```

y **todos los usuarios tenían que volver a cargar su API key a mano** — lo cual es
especialmente doloroso porque OpenProject muestra la clave **una sola vez**: el usuario ni
siquiera puede recuperar la anterior, tiene que generar una nueva.

Con el ring en la base, un solo `pg_dump` se lleva las dos mitades. No se pueden
desincronizar porque ya no son dos cosas.

### El certificado (opcional pero recomendado en producción)

La contrapartida de meter el ring en la base es que un dump filtrado traería las claves
en claro **junto a los datos que protegen** — o sea, el cifrado dejaría de valer contra
cualquiera que consiga una copia del backup.

Para taparlo, `ProtectKeysWithCertificate` envuelve el ring con un `.pfx` que vive fuera
de la base. A diferencia del ring (que rota solo cada 90 días), el certificado es un
archivo **estático**: se respalda una vez y no vuelve a cambiar.

Generarlo:

```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 3650 -nodes \
  -subj "/CN=TrackingTasksOp KeyRing"
openssl pkcs12 -export -out Deploy/keyring.pfx -inkey key.pem -in cert.pem \
  -passout pass:LA_PASSWORD
rm key.pem cert.pem
```

Después, en `docker-compose.yml`, descomentar las dos variables
`DataProtectionSettings__KeyRingCertificate*` y el volumen que monta el `.pfx`, y agregar
`KEYRING_CERT_PASSWORD` al `.env`.

> **Si se pierde el `.pfx`, el ring queda ilegible y volvés al problema original.**
> Respaldalo una vez, fuera del dump de la base (gestor de contraseñas o almacenamiento
> aparte). Guardarlo *dentro* del mismo backup que la base anula el propósito.

## Backup

Con el ring en la base, el backup es un solo artefacto:

```bash
docker exec trackingtasksop-postgres-1 pg_dump -U trackingtasks TrackingTasksDb > backup.sql
```

Ese archivo contiene ya las API keys cifradas **y** las claves para descifrarlas. Dos
consecuencias:

- **Restaurarlo funciona solo**, sin coordinar nada más (ese era el objetivo).
- **Tratalo como material sensible.** Sin el certificado, quien tenga el dump tiene las
  API keys de OpenProject de todos los usuarios.

Probá el restore de verdad al menos una vez. Un backup no verificado no es un backup.

### Importante: no cambiar `ApplicationName` a la ligera

El valor de `ApplicationName` se usa como "discriminador" en la derivación de claves.
Si lo cambias, **los datos cifrados con el `ApplicationName` anterior dejan de poder
descifrarse**, aunque la clave siga existiendo en la tabla. Si necesitas renombrarlo,
los usuarios existentes tendrán que volver a ingresar su API key de OpenProject.

## Historial: por qué el key ring no está en `Web/Keys/`

Antes de vivir en la base, el `KeyRingPath` era una ruta de **Windows hardcodeada**
(`"C:\\ProgramData\\TrackingTaskOp\\Keys"`). En Linux/macOS `\` no es separador de
directorios, así que .NET interpretaba toda esa cadena como **un solo nombre de carpeta
literal** y la creaba dentro de `Web/`:

```
Web/C:\ProgramData\TrackingTaskOp\Keys/
```

Se evaluó `Environment.SpecialFolder.LocalApplicationData`, pero en Linux esa ruta depende
de `XDG_DATA_HOME`, que algunas terminales (p. ej. VS Code instalado como *snap*)
sobreescriben — mismo problema de fondo, distinto disfraz. Se pasó entonces a `Web/Keys/`
(relativo al `ContentRootPath`), que resolvió la inestabilidad de la ruta pero no el
problema de fondo: **seguían siendo dos artefactos que un backup podía separar.** Eso es
lo que resuelve la persistencia en base.
