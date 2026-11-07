# Configuración del correo (formulario de contacto)

El formulario de contacto envía dos correos con MailKit (uno al administrador y una confirmación a quien escribe).
Desde la migración a .NET 10 la configuración ya no vive en `Web.config` sino en variables de entorno o en un JSON local.

## Claves requeridas

| Clave | Ejemplo | Descripción |
|---|---|---|
| `SMTP_HOST` | `smtp.tu-proveedor.com` | Servidor SMTP |
| `SMTP_PORT` | `465` | Puerto (465 SSL implícito, 587 STARTTLS) |
| `SMTP_USER` | `usuario@tu-dominio.com` | Usuario |
| `SMTP_PASSWORD` | (secreto) | Contraseña |
| `SMTP_FROM_EMAIL` | `no-reply@tu-dominio.com` | Remitente |
| `ADMIN_EMAIL` | `contacto@tu-dominio.com` | Quien recibe los mensajes |
| `SMTP_ENABLE_SSL` | `true` | SSL/TLS |

Opcional: `ObsUsoRetencionDias` (días que se conservan los eventos de analítica anónima; 180 por defecto, mínimo 30).

## Dónde se definen (prioridad de mayor a menor)

1. **Variable de entorno** del servidor con el mismo nombre (producción). Es lo recomendable.
2. **`secretsSettings.json`** junto a la aplicación (desarrollo local). Copie `secretsSettings.json.example` y complete los valores. Está en `.gitignore` y **nunca viaja en la publicación**.
3. `appsettings.json` (valores vacíos por defecto; no ponga secretos aquí).

Si falta una clave requerida, el formulario responde "Error de configuración del servidor" y la excepción dice cuál falta.

## Hosting compartido (Plesk / IIS)

Si el panel no permite variables de entorno, cree un `secretsSettings.json` directamente en la carpeta del sitio en el servidor
(no lo suba a git ni lo incluya en el paquete publicado). Reinicie el sitio después de cambiarlo.

## Seguridad

- No suba credenciales a git (`secretsSettings.json` está ignorado).
- El proyecto excluye `secretsSettings.*` del contenido publicado (`bufinsweb.csproj`).
- Rote la contraseña SMTP si alguna vez estuvo en un repositorio, un paquete publicado o un chat.
