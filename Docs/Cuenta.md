# Mi cuenta: foto de perfil, contraseña y verificación en dos pasos

La vista `/settings.html` reúne toda la configuración: tu cuenta, las notificaciones, la
apariencia, el comportamiento de las tareas y las integraciones.
Se llega desde el botón **Ajustes** de la barra superior del dashboard, o desde
**Mi cuenta** en el menú de tu avatar.

Cada sección tiene su propia dirección (`#cuenta`, `#seguridad`, `#notificaciones`,
`#apariencia`, `#tareas`, `#openproject`, `#asistente`), así que se pueden compartir enlaces
directos y el botón *atrás* del navegador funciona.

La primera parte de este documento es para cualquiera que use la app.
La [referencia técnica](#referencia-técnica) del final es para quien toque el código.

---

## Para usar la app

### Foto de perfil

Elegí una imagen y se sube sola. El navegador la recorta al centro y la achica a 256px antes
de enviarla, así que no importa si la original pesa varios megas.

Aceptamos JPEG, PNG y WebP; todo se guarda como JPEG. Si no cargás ninguna, el menú muestra
las dos primeras letras de tu correo.

Para volver a las iniciales, **Quitar foto**.

### Verificación en dos pasos (2FA)

Es un código de 6 dígitos que cambia cada 30 segundos y lo genera una app en tu teléfono.
**Hace falta para cambiar tu contraseña.** Para todo lo demás — cargar tareas, iniciar y
terminar sesiones, usar el bot — no se pide nunca.

#### Qué necesitás

Cualquier app que soporte TOTP, que es el estándar:

- Google Authenticator
- Microsoft Authenticator
- Authy
- 2FAS o Aegis (Android, código abierto)
- O tu gestor de contraseñas, si usás Bitwarden o 1Password

#### Cómo activarla

1. Entrá a **Ajustes → Seguridad**. En la tarjeta "Verificación en dos pasos" vas a ver el código QR.
2. Abrí la app del teléfono y escaneá el QR.
   Si la cámara no coopera, debajo del QR está la misma clave en texto para cargarla a mano.
3. La app te muestra un número de 6 dígitos. Escribilo en el campo y dale **Activar**.
4. Aparecen tus **códigos de recuperación**. Guardalos (ver abajo).

#### Los códigos de recuperación

Son 8 códigos de un solo uso que valen **en lugar** del código de la app. Sirven cuando no
tenés el teléfono a mano.

Guardalos donde guardes tus contraseñas. No se vuelven a mostrar, **pero podés generar una
tanda nueva cuando quieras** desde la misma pantalla: si los perdiste o no estás seguro de
dónde quedaron, generás otros y los anteriores dejan de servir.

### API key del asistente de IA

El bot funciona sin que configures nada: usa una key compartida del servidor, con un **límite
diario de mensajes por persona**.

Si ese límite te queda corto, podés poner tu propia API key de Groq y el límite deja de
aplicarte — a cambio, el consumo corre por tu cuenta. Se consigue gratis en
[console.groq.com](https://console.groq.com) → API Keys → Create API Key.

La key se guarda cifrada y **nunca se vuelve a mostrar**, ni siquiera a vos: por eso el campo
aparece siempre vacío. El estado se ve en la etiqueta de la tarjeta ("Key compartida" o "Key
propia"). Para volver atrás, **Quitar y volver a la compartida**.

> Antes esto estaba en la barra lateral del dashboard, junto con el resto de la
> configuración. Todo eso se mudó acá y el sidebar desapareció: esos 272px de ancho
> ahora son para las tarjetas de tareas.

### Cambiar la contraseña

En **Ajustes → Seguridad**, tarjeta **Contraseña**: la actual, la nueva dos veces, y un código de verificación.
Mínimo 12 caracteres.

En el campo del código vale tanto el número de la app como uno de recuperación.

Si todavía no activaste el 2FA, la tarjeta te lo avisa en vez de mostrarte un formulario
que no va a funcionar: activalo primero, arriba.

### Cambiar de teléfono

Si cambiás de celular, no hace falta hacer nada raro: **Cambiar de teléfono** desvincula la
app actual y te deja volver a escanear el QR con el dispositivo nuevo.

Pide un código (de la app o de recuperación) y tu contraseña. Al desvincular, la app del
teléfono viejo deja de servir aunque siga instalada.

---

## Si perdiste el acceso

### Perdí el teléfono pero tengo los códigos de recuperación

Sin problema. El código de recuperación funciona en cualquiera de los tres lugares donde se
pide un código, incluido **Cambiar de teléfono**. Usalo ahí, desvinculás, y enrolás el celular
nuevo.

### Perdí el teléfono y no tengo los códigos

**No perdés la cuenta ni tus datos.** Seguís entrando con tu correo y contraseña como siempre,
y podés usar toda la app: tareas, sesiones, reportes, bot.

Lo que no vas a poder hacer hasta que alguien te destrabe:

| Acción | ¿Funciona? |
|---|---|
| Iniciar sesión | Sí |
| Cargar tareas, iniciar/terminar sesiones | Sí |
| Usar el bot | Sí |
| Cambiar tu foto de perfil | Sí |
| **Cambiar tu contraseña** | No |
| **Generar códigos de recuperación** | No |
| **Desvincular el teléfono** | No |

Escribile a quien administre la instancia para que te resetee el segundo factor.

### Reset del 2FA (para quien administra la instancia)

> **Pendiente:** todavía no hay pantalla de administrador para esto. Como el proyecto no
> tiene envío de correo, la única salida hoy es tocar la base a mano. Queda anotado como
> deuda.

```sql
UPDATE AspNetUsers SET TwoFactorEnabled = 0 WHERE Email = 'usuario@ejemplo.com';

DELETE FROM AspNetUserTokens
WHERE UserId = (SELECT Id FROM AspNetUsers WHERE Email = 'usuario@ejemplo.com')
  AND Name IN ('AuthenticatorKey', 'RecoveryCodes');
```

Después de eso, la persona entra a **Ajustes → Seguridad** y el flujo le ofrece enrolar de nuevo.

---

## Referencia técnica

### Endpoints — `api/v1/account`

Todos exigen sesión iniciada (el `AuthorizeFilter` global de `Program.cs`).
Los de contraseña y 2FA llevan además `[EnableRateLimiting("auth")]`.

| Verbo | Ruta | Qué hace |
|---|---|---|
| `POST` | `2fa/setup` | Devuelve el QR (data URI) y la clave manual. No activa nada |
| `POST` | `2fa/enable` | Valida el código, activa el 2FA y devuelve los códigos de recuperación |
| `POST` | `2fa/recovery-codes` | Emite códigos nuevos e invalida los anteriores |
| `POST` | `2fa/reset` | Desvincula la app y desactiva el 2FA. Pide contraseña + código |
| `PUT` | `password` | Cambia la contraseña. Pide la actual + código |
| `PUT` | `avatar` | Sube el avatar (JPEG en base64) |
| `DELETE` | `avatar` | Borra el avatar |
| `GET` | `avatar` | Devuelve los bytes. `404` si no tiene |

La API key de IA es la excepción: sigue en `PUT api/v1/settings/ai-api-key`, donde ya estaba.
Se movió la **UI** de la barra lateral a esta vista, no el endpoint.

El rate limit no es decorativo: **Identity no bloquea la cuenta ante códigos TOTP fallidos**,
así que la política `auth` (10 por minuto por IP) es lo único que frena la fuerza bruta sobre
un número de 6 dígitos.

### Modelo de datos

El 2FA **no agregó ni una columna**. `TwoFactorEnabled` ya venía de `IdentityUser`, la clave
del authenticator y los códigos de recuperación viven en `AspNetUserTokens`, y
`AddDefaultTokenProviders()` ya estaba configurado en `Web/Extensions/IdentityExtensions.cs`.

Lo único nuevo es la tabla `UserAvatars` (migración `AddUserAvatar`):

| Columna | Tipo | Notas |
|---|---|---|
| `UserId` | `nvarchar(450)` | PK y FK a `AspNetUsers`, borrado en cascada |
| `Jpeg` | `varbinary(max)` | Imagen ya redimensionada, ~15KB |
| `UpdatedAt` | `datetime2` | Alimenta el `Last-Modified` de `GET avatar` |

### Decisiones que conviene no revertir sin leer esto

**El avatar va en tabla aparte, no como columna de `ApplicationUser`.**
`FindByIdAsync` corre en caliente — `AiUsageLimiterImpl` y `GroqAuthHeaderProvider` lo llaman
en cada mensaje del bot. Con el blob en esa entidad, EF arrastraría los bytes de la imagen en
todas esas consultas.

**El avatar va en la base, no en disco.**
Así viaja con el dump al migrar al VPS y no suma otro volumen que declarar en el container
—y que olvidar en la mudanza.

**El redimensionado ocurre en el navegador.**
Evita meter una librería de imágenes en el backend. Pero el servidor igual valida tamaño
(512KB) y magic bytes de JPEG: el cliente puede mentir, y esos bytes terminan sirviéndose a
un navegador.

**`RefreshSignInAsync` después de cambiar la contraseña.**
`ChangePasswordAsync` rota el `SecurityStamp`, que invalida las cookies existentes — incluida
la de la sesión actual. Sin ese refresh, cambiar la contraseña te expulsa al login en el acto.

**El login no exige segundo factor.**
`LoginLocalUserCommandImpl` usa `CheckPasswordSignInAsync`, que valida contraseña y lockout
pero no dispara el flujo de 2FA. Es deliberado: activar el 2FA no puede dejar afuera a nadie.
Si algún día se quiere 2FA en el login, hay que cambiarlo por `PasswordSignInAsync` y manejar
`RequiresTwoFactor` — y recién ahí el caso "perdí todo" pasa a bloquear el acceso completo.

**El QR se genera en el servidor con QRCoder**, usando `PngByteQRCode` y no `QRCode`: este
último depende de `System.Drawing`, que en Linux exige `libgdiplus`. Como la app apunta a
correr en un container, esa dependencia no puede entrar.

### Archivos

```
Web/wwwroot/settings.html                       vista de ajustes (standalone, como bot.html)
Web/wwwroot/js/settings.js                      lógica de la vista
Web/wwwroot/js/settings-fields.js               notificaciones, apariencia y tareas
Web/wwwroot/js/avatar.js                        avatar compartido con el sidebar
Web/Controllers/AccountController.cs            endpoints
Application/Ports/UseCases/Account/             puertos
Infrastructure/Adapters/UseCases/Account/       implementaciones
Infrastructure/Adapters/Services/QrCodeServiceImpl.cs
Infrastructure/DataAccess/Entities/UserAvatar.cs
```

### Deuda conocida

- **No hay reset de 2FA por administrador.** Hoy es SQL a mano (ver arriba).
- **Faltan las pruebas**: que se rechace el TOTP incorrecto, la contraseña actual incorrecta,
  la contraseña incorrecta en el reset, y que el avatar rechace lo que no sea JPEG.
