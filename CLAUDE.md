# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Bufins Web is an informational/marketing website for Bufins, a financial business intelligence platform. Built with ASP.NET MVC 5 on .NET Framework 4.8. The site is in Spanish (es-CO locale) and consists of three pages: Home, Services (About), and Contact. There is no database — the only backend functionality is a contact form that sends emails via SMTP.

## Build & Run

1. Open `bufinsweb.sln` in Visual Studio
2. Restore NuGet Packages (right-click solution → Restore NuGet Packages)
3. Build: `Ctrl+Shift+B` or Build → Rebuild Solution
4. Run: F5 (IIS Express) → opens at `https://localhost:44337/`

There is no CLI build command — this project uses MSBuild via Visual Studio. If needed from command line:
```
msbuild bufinsweb.csproj /p:Configuration=Debug
```

## Architecture

**Single controller pattern:** `HomeController.cs` handles all routes (Index, About, Contact GET/POST).

**Request flow for contact form:**
```
Contact.cshtml (AJAX POST with AntiForgeryToken)
  → HomeController.Contact() [POST, ValidateAntiForgeryToken]
    → ContactFormModel (validation via data annotations)
      → EmailService.SendContactFormToAdmin() (HTML email to admin)
      → EmailService.SendConfirmationToUser() (HTML email to submitter)
        → JSON response (success/failure)
```

**Key files:**
- `Controllers/HomeController.cs` — all page actions and contact form POST handler
- `Models/ContactFormModel.cs` — form model with Required/Email/Phone validation attributes
- `Services/EmailService.cs` — MailKit-based SMTP email service with HTML templates
- `Helpers/EnvironmentHelper.cs` — reads config from Web.config appSettings (GetRequiredVariable, GetOptionalVariable, GetBooleanVariable, GetIntVariable)
- `Views/Shared/_Layout.cshtml` — master layout (navbar, footer, scripts)
- `Assets/css/bufins-custom.css` — all custom styling (~1700 lines, uses CSS custom properties)
- `App_Start/BundleConfig.cs` — CSS/JS bundling configuration
- `App_Start/RouteConfig.cs` — URL routing (default: `{controller}/{action}/{id}`)

## Configuration

SMTP settings are in `Web.config` under `<appSettings>`. Use `Web.config.example` as a template. See `CONFIGURACION_CORREO.md` for detailed email setup instructions.

Required keys: `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM_EMAIL`, `ADMIN_EMAIL`, `SMTP_ENABLE_SSL`

## Conventions

- **Language:** All UI text, model validation messages, and comments are in Spanish
- **Naming:** PascalCase for C# classes/methods, kebab-case for CSS classes
- **Styling:** Bootstrap 5 + custom CSS with CSS variables defined in `:root` (see `bufins-custom.css`)
- **Design palette:** Dark Purple `#160933`, Cyan `#01E1D7`, Purple `#583AFF`, Orange `#FF5500`
- **Animations:** AOS (Animate On Scroll) library for scroll-triggered animations
- **No tests, no linter, no CI/CD** — manual deployment via Visual Studio Web Deploy

## Observatorio financiero (`/observatorio`)

Modulo publico de analisis de ~37 mil empresas colombianas (estados financieros NIIF reportados a la Supersociedades, datos.gov.co). No usa base de datos: lee JSON de `App_Data/observatorio/` en memoria.

- Paginas: `Views/Observatorio/` (Index, Sectores, Comparador, Empresa, Nodo). API JSON en `/observatorio/api/{accion}` (`ObservatorioController`). Logica en `Services/ObservatorioService.cs`, indicadores en `Models/Observatorio/ObservatorioModels.cs` (clase `Ratio`), catalogo CIIU en `Helpers/CiiuCatalogo.cs`.
- Front: `Assets/js/obs-*.js` + Chart.js local; estilos en `Assets/css/observatorio.css`. Textos bilingues inline con `ObservatorioFormat.T(es, en)` (no usan los .resx).
- Datos: ETL en C# (`Tools/ObservatorioEtl/`, sin dependencias). Compilar: `msbuild Tools\ObservatorioEtl\ObservatorioEtl.csproj /restore /p:Configuration=Release`. Ejecutar: `ObservatorioEtl.exe --out <bufinsweb>\App_Data\observatorio [--force] [--years 2024 2025] [--cache DIR]` (~25-30 min; con `--cache` reutiliza descargas). Genera `empresas.bin` (indice binario en memoria), `meta.json`, `detalle.{version}.bin` y `detalle.{version}.idx.json` (versionados; NO se versionan en git; se publican con Web Deploy como contenido) y escribe el avance en `etl_status.json`. La publicacion es atomica (empresas.bin se reemplaza al final; el sitio recarga solo). Si la fuente no cambio, termina en segundos ("sin_cambios"). El sitio NO consulta datos.gov.co en vivo. `ciiu.json` se genera una vez con `build_ciiu.py` (Python, desde el PDF del DANE).
- Actualizar desde el portal: `bufinscustomers` > Configuracion > Datos del Observatorio (solo Super Admin) lanza `ObservatorioEtl.exe` como proceso aparte (claves `ObservatorioEtlExe` y `ObservatorioDatosDir` en el Web.config del portal; mismo servidor IIS).
- Refresco recomendado: manual, cuando Supersociedades actualice la fuente (anual); la pagina muestra la fecha de la fuente.
- Rendimiento: los datos se precargan en `Application_Start` (hilo de fondo); indices por clase CIIU y conteos por nodo se calculan al cargar. La API publica (`/observatorio/api/*`) tiene limite por IP (`Filters/ThrottleAttribute.cs`) y cache de salida versionada con `v` (version de los datos, ver `_ObsNav`). JS/CSS del Observatorio salen por `BundleConfig` (`~/bundles/obs-*`), minificados y con `?v=hash`.
- Fuentes externas: solo API Socrata de datos.gov.co (datasets `6hqw-m3dm`, `pfdp-zks5`, `prwj-nzxa`, `ctcp-462n`) durante el ETL, y el PDF CIIU del DANE (una vez, para `build_ciiu.py`).
