# Configuración del Sistema de Envío de Correos

Este documento explica cómo configurar las credenciales SMTP en Web.config para que el formulario de contacto funcione correctamente.

## 📋 Configuración Requerida

El sistema de envío de correos requiere las siguientes claves en la sección `<appSettings>` del archivo **Web.config**:

| Clave | Descripción | Ejemplo |
|-------|-------------|---------|
| `SMTP_HOST` | Servidor SMTP de tu proveedor | `mail.tudominio.com` |
| `SMTP_PORT` | Puerto SMTP (generalmente 587 para TLS) | `587` |
| `SMTP_USER` | Usuario para autenticación SMTP | `correo@tudominio.com` |
| `SMTP_PASSWORD` | Contraseña del usuario SMTP | `tu_contraseña_segura` |
| `SMTP_FROM_EMAIL` | Correo que aparecerá como remitente | `noreply@tudominio.com` |
| `ADMIN_EMAIL` | Tu correo donde recibirás las notificaciones | `admin@tudominio.com` |
| `SMTP_ENABLE_SSL` | Habilitar SSL/TLS (true/false) | `true` |

---

## ⚙️ Paso 1: Configurar Web.config

### Opción A: Editar Web.config directamente

Abre el archivo `Web.config` y reemplaza los valores de ejemplo con tus credenciales reales:

```xml
<appSettings>
  <!-- ... otras configuraciones ... -->

  <!-- Configuración SMTP para el formulario de contacto -->
  <add key="SMTP_HOST" value="mail.tudominio.com" />
  <add key="SMTP_PORT" value="587" />
  <add key="SMTP_USER" value="correo@tudominio.com" />
  <add key="SMTP_PASSWORD" value="tu_contraseña_real" />
  <add key="SMTP_FROM_EMAIL" value="noreply@tudominio.com" />
  <add key="ADMIN_EMAIL" value="admin@tudominio.com" />
  <add key="SMTP_ENABLE_SSL" value="true" />
</appSettings>
```

### Opción B: Usar Web.config.example como plantilla

1. El archivo `Web.config.example` contiene ejemplos de configuración
2. Copia los valores que necesites y pégalos en tu `Web.config`
3. Reemplaza los valores de ejemplo con tus credenciales reales

---

## 📧 Configuración por Proveedor SMTP

### Servidor SMTP Propio (Hosting)

```xml
<add key="SMTP_HOST" value="mail.tudominio.com" />
<add key="SMTP_PORT" value="587" />
<add key="SMTP_USER" value="correo@tudominio.com" />
<add key="SMTP_PASSWORD" value="tu_contraseña" />
<add key="SMTP_FROM_EMAIL" value="noreply@tudominio.com" />
<add key="ADMIN_EMAIL" value="admin@tudominio.com" />
<add key="SMTP_ENABLE_SSL" value="true" />
```

**Nota:** La mayoría de hostings te proporcionan estos datos en el panel de control (cPanel, Plesk, etc.)

### Gmail (solo para pruebas)

```xml
<add key="SMTP_HOST" value="smtp.gmail.com" />
<add key="SMTP_PORT" value="587" />
<add key="SMTP_USER" value="tucorreo@gmail.com" />
<add key="SMTP_PASSWORD" value="xxxx xxxx xxxx xxxx" />
<add key="SMTP_FROM_EMAIL" value="tucorreo@gmail.com" />
<add key="ADMIN_EMAIL" value="tucorreo@gmail.com" />
<add key="SMTP_ENABLE_SSL" value="true" />
```

**Importante para Gmail:**
1. Debes tener la verificación en dos pasos activada
2. Ve a: [https://myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
3. Genera una "Contraseña de aplicación" para "Correo"
4. Usa esa contraseña de 16 caracteres (sin espacios) en `SMTP_PASSWORD`

### Outlook/Hotmail

```xml
<add key="SMTP_HOST" value="smtp-mail.outlook.com" />
<add key="SMTP_PORT" value="587" />
<add key="SMTP_USER" value="tucorreo@outlook.com" />
<add key="SMTP_PASSWORD" value="tu_contraseña" />
<add key="SMTP_FROM_EMAIL" value="tucorreo@outlook.com" />
<add key="ADMIN_EMAIL" value="tucorreo@outlook.com" />
<add key="SMTP_ENABLE_SSL" value="true" />
```

---

## 🔒 Paso 2: Encriptar Web.config (RECOMENDADO para producción)

Para mayor seguridad, puedes encriptar la sección `<appSettings>` en producción.

### En tu Servidor de Producción (Windows)

1. **Abre CMD como Administrador** en tu servidor

2. **Navega a la carpeta .NET Framework:**
   ```cmd
   cd C:\Windows\Microsoft.NET\Framework64\v4.0.30319
   ```

3. **Encripta la sección appSettings:**
   ```cmd
   aspnet_regiis -pef "appSettings" "C:\ruta\completa\a\tu\sitio"
   ```

   **Ejemplo:**
   ```cmd
   aspnet_regiis -pef "appSettings" "C:\inetpub\wwwroot\bufinsweb"
   ```

4. **Resultado:** El Web.config ahora tiene el `<appSettings>` encriptado:
   ```xml
   <appSettings configProtectionProvider="RsaProtectedConfigurationProvider">
     <EncryptedData Type="http://www.w3.org/2001/04/xmlenc#Element">
       <EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#tripledes-cbc" />
       <KeyInfo xmlns="http://www.w3.org/2000/09/xmldsig#">
         <EncryptedKey xmlns="http://www.w3.org/2001/04/xmlenc#">
           <!-- Datos encriptados -->
         </EncryptedKey>
       </KeyInfo>
     </EncryptedData>
   </appSettings>
   ```

5. **La aplicación funciona igual:** ASP.NET desencripta automáticamente al leer

### Desencriptar (si necesitas editar)

```cmd
aspnet_regiis -pdf "appSettings" "C:\ruta\completa\a\tu\sitio"
```

---

## 🧪 Paso 3: Probar la Configuración

1. **Reinicia la aplicación** (reinicia IIS o el sitio web)
2. Ve a la página de **Contacto** en tu sitio
3. Llena el formulario y envíalo
4. Deberías recibir:
   - ✅ Un correo en tu bandeja de entrada (ADMIN_EMAIL) con los datos del formulario
   - ✅ Un correo de confirmación en el correo del usuario que llenó el formulario

---

## 🐛 Solución de Problemas

### Error: "La configuración 'SMTP_HOST' es requerida pero no está configurada"

**Causa:** No has configurado las claves en Web.config

**Solución:**
1. Abre `Web.config`
2. Verifica que todas las claves SMTP estén presentes en `<appSettings>`
3. Reemplaza `TU_SERVIDOR_SMTP` con valores reales
4. Guarda el archivo
5. Reinicia IIS o el sitio

### Error: "Ha ocurrido un error al enviar el mensaje"

**Causas posibles:**
- Credenciales SMTP incorrectas
- Puerto bloqueado por firewall
- SSL mal configurado

**Solución:**
1. Verifica usuario y contraseña SMTP
2. Prueba con `SMTP_ENABLE_SSL` en `true` o `false`
3. Confirma el puerto (587 para TLS, 465 para SSL)
4. Verifica que el firewall del servidor no bloquee el puerto SMTP

### Los correos no llegan

**Causas posibles:**
- Correos en spam
- SPF/DKIM no configurados
- Correo remitente no autorizado

**Solución:**
1. Revisa carpeta de spam/correo no deseado
2. Usa un correo válido de tu dominio en `SMTP_FROM_EMAIL`
3. Configura SPF y DKIM en tu dominio (consulta con tu proveedor de hosting)
4. Algunos hostings requieren que el remitente sea una cuenta real del dominio

### Error en el hosting

**Causa:** Algunos hostings compartidos tienen restricciones SMTP

**Solución:**
1. Contacta al soporte de tu hosting
2. Pregunta por la configuración SMTP correcta
3. Algunos hostings requieren usar su servidor SMTP específico
4. Verifica que tu plan permita envío de correos

---

## 🔐 Seguridad y Buenas Prácticas

### ✅ Recomendaciones de Seguridad

1. **NO subas Web.config con credenciales a Git**
   - Agrega `Web.config` a `.gitignore`
   - Solo sube `Web.config.example`

2. **Encripta Web.config en producción**
   - Usa `aspnet_regiis` para encriptar `<appSettings>`

3. **Usa contraseñas fuertes**
   - Contraseñas únicas para SMTP
   - Cámbialas regularmente

4. **Limita permisos de archivo**
   - En el servidor, configura permisos de solo lectura para Web.config

5. **Considera usar una cuenta dedicada**
   - Usa `noreply@tudominio.com` solo para envío de correos
   - No uses tu correo personal principal

### 📝 Para Control de Versiones (Git)

Agrega esto a tu `.gitignore`:

```gitignore
# No versionar Web.config con credenciales
Web.config

# Sí versionar el ejemplo
!Web.config.example
```

---

## ✨ Características Implementadas

- ✅ Configuración centralizada en Web.config
- ✅ Soporte para encriptación con aspnet_regiis
- ✅ Doble envío: notificación al admin + confirmación al usuario
- ✅ Templates HTML personalizados con colores de Bufins
- ✅ Validación completa (cliente y servidor)
- ✅ Mensajes dinámicos de éxito/error
- ✅ Seguridad con antiforgery token
- ✅ Manejo robusto de errores
- ✅ Compatible con cualquier hosting ASP.NET

---

## 📞 Soporte

Si tienes problemas con la configuración:

1. ✅ Verifica que todas las claves estén en `<appSettings>`
2. ✅ Confirma que los valores no sean de ejemplo (`TU_SERVIDOR_SMTP`)
3. ✅ Reinicia IIS o el sitio después de cambiar Web.config
4. ✅ Revisa los logs de IIS para errores específicos
5. ✅ Contacta al soporte de tu hosting si es necesario

---

**Última actualización:** 2025-11-12
**Versión:** 2.0 (Web.config)
