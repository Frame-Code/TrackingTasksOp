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

Las claves criptográficas (los archivos `key-<guid>.xml`) se guardan en
**`Web/Keys/`**, dentro del propio proyecto (`ContentRootPath`), independientemente
del SO, de la terminal o del usuario que ejecute la app.

**No se necesita configurar ninguna ruta** — la carpeta se crea sola en el primer
arranque (`Directory.CreateDirectory`). `Web/Keys/` está en `.gitignore`: es local
de cada copia del repo y nunca debe commitearse.

```jsonc
// Web/appsettings.json
"DataProtectionSettings": {
  "ApplicationName": "TrackingTaskOp"
}
```

## ¿Por qué se cambió esto?

Antes, `DataProtectionSettings` tenía además un campo `KeyRingPath` con una ruta de
**Windows hardcodeada** (`"C:\\ProgramData\\TrackingTaskOp\\Keys"`).

En Linux/macOS, `\` no es separador de directorios, así que .NET interpretaba toda esa
cadena como **un solo nombre de carpeta literal** (con `:` y `\` incluidos) y la creaba
dentro de `Web/`, generando carpetas con nombres como:

```
Web/C:\ProgramData\TrackingTaskOp\Keys/
```

Si alguien cambiaba ese valor (por ejemplo, de `TrackingTaskOp` a `TrackingTaskOpDevelop`),
se generaba **otra carpeta nueva con otra clave**, y el API key cifrado anteriormente
en la base de datos dejaba de poder descifrarse:

```
System.Security.Cryptography.CryptographicException: The key {xxxxxxxx-...} was not found in the key ring.
```

Se evaluó primero usar `Environment.SpecialFolder.LocalApplicationData`
(`~/.local/share` en Linux, `%LOCALAPPDATA%` en Windows), pero en Linux esa ruta
depende de la variable de entorno `XDG_DATA_HOME`, que algunas terminales
(p. ej. la integrada de VS Code instalado como *snap*) sobreescriben a una ruta
distinta (`~/snap/code/<rev>/.local/share`). Esto provocaba que, según desde qué
terminal se ejecutara `dotnet run`, la app generara/buscara el keyring en una
carpeta distinta — mismo problema de fondo, distinto disfraz.

La solución final usa `Web/Keys/` (relativo al `ContentRootPath` del proyecto):
siempre la misma ruta, sin importar el SO, la terminal, variables de entorno o el
usuario que ejecute la app.

## Configuración para un dev nuevo

**No requiere ninguna configuración manual.** Al clonar el repo y ejecutar la app por
primera vez:

1. `Directory.CreateDirectory` crea automáticamente `Web/Keys/` si no existe.
2. ASP.NET Core genera ahí una clave nueva la primera vez que se necesita cifrar/descifrar
   algo.
3. Mientras `DataProtectionSettings:ApplicationName` no cambie y `Web/Keys/` no se borre,
   los API keys de OpenProject que el usuario registre seguirán siendo descifrables en
   reinicios sucesivos de la app, sin importar desde qué terminal se ejecute.

### Importante: no cambiar `ApplicationName` a la ligera

El valor de `ApplicationName` se usa como "discriminador" en la derivación de claves.
Si lo cambias, **los datos cifrados con el `ApplicationName` anterior dejan de poder
descifrarse**, aunque el archivo de clave siga existiendo. Si necesitas renombrarlo,
los usuarios existentes tendrán que volver a ingresar su API key de OpenProject
(re-registro de credenciales).

### Si perdiste el acceso a tu key ring (p. ej. borraste la carpeta)

Vas a ver el mismo `CryptographicException` al intentar listar proyectos / work
packages. La única solución es volver a registrar tu API key de OpenProject
(re-login/registro local), ya que la clave para descifrar la anterior se perdió.

## Despliegue como servicio (producción)

Al correr como **servicio de Windows**, `ContentRootPath` es la carpeta donde se
publicó la app (`./publish`), así que el keyring quedará en `./publish/Keys`.
Asegúrate de que la cuenta con la que corre el servicio tenga permisos de escritura
sobre esa carpeta.

Si en el futuro se necesita **compartir el key ring entre varias instancias/máquinas**
(por ejemplo, balanceo de carga), se debe volver a introducir una ruta explícita
configurable (`KeyRingPath`) apuntando a un recurso compartido, y considerar
`ProtectKeysWithCertificate` en vez de `PersistKeysToFileSystem` para no depender del
sistema de archivos local. Eso queda fuera del alcance de la configuración actual
(pensada para desarrollo local de un solo dev).
