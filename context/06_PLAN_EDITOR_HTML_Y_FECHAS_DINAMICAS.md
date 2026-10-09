# 06 — PLAN: Editor HTML para Outlook + Campos y Fechas Dinámicas

> Documento de seguimiento de implementación.
> Estado: **PENDIENTE DE EJECUCIÓN**.
> Fecha de redacción: 09/10/2026.

---

## 1. Resumen ejecutivo

Tres capacidades nuevas, todas en la configuración de correo por pauta:

1. **Cuerpo enriquecido solo para Outlook.** Cuando `EmailMethod == Outlook` el usuario edita con
   formato (negrita, color, listas, tablas, imágenes inline). Cuando `EmailMethod == Mailto` se
   conserva el `TextBox` de texto plano actual, sin ningún cambio.
2. **Botón "Agregar campo dinámico"** en ambos editores. Inserta `[Etiqueta]` en la posición del
   caret, eligiendo de la lista de campos de la pauta actual.
3. **Fechas dinámicas del sistema** (semana, mes, año, rango custom), independientes de la pauta,
   para títulos como `Auditorías de llamadas del 03/10/2026 al 09/10/2026`.

---

## 2. Hallazgo clave (ya funciona hoy)

`Services/EmailService.cs:304-354` → `SendViaOutlook` **ya asigna `mailItem.HTMLBody`**:

```csharp
if (IsHtml(body))
    mailItem.HTMLBody = body;   // ya funciona: negritas, colores, tablas
else
    mailItem.Body = body;
```

E `IsHtml()` (línea 359-366) ya reconoce `<table`. Es decir: **el canal de transporte ya existe**.
Outlook usa COM tardío (`Type.GetTypeFromProgID`), sin referencia a Interop.

Lo que falta es (a) editor visual, (b) blindar valores contra HTML roto, (c) imágenes inline por
Content-ID, y (d) los asistentes de campos y fechas.

### Tabla comparativa

| | mailto | Outlook |
|---|---|---|
| Mecanismo | URI RFC 6068 `mailto:...?body=` | Objeto COM `MailItem` |
| Cuerpo | Solo texto plano | `.Body` / `.HTMLBody` / RTF |
| Formato | Imposible | Negrita, color, listas, tablas, imágenes |
| Límite | ~2000 chars (ya se avisa en UI) | Sin límite práctico |

**Restricción del motor:** Outlook renderiza con Word y **descarta** `data:image;base64,...`,
atributos `class` y bloques `<style>`. Todo el formato debe ir **inline**. Las imágenes inline
requieren Content-ID (`cid:`).

---

## 3. Decisión de diseño: dos campos separados

Sin migración. El campo existente no se toca; el nuevo arranca vacío.

| Método | Campo del modelo | Editor |
|---|---|---|
| `Mailto` | `EmailBodyTemplate` (existente) | `TextBox` actual, intacto |
| `Outlook` | `EmailBodyHtmlTemplate` (**nuevo**) | `RichHtmlEditor` (WebView2) |

Consecuencia: **el paso de "strip de etiquetas al enviar por mailto" se elimina**. Al estar
separados, mailto no puede recibir HTML.

**Consecuencia aceptada:** las pautas configuradas en Outlook deberán reescribir su cuerpo una vez.

---

## 4. Decisiones tomadas por el usuario

| Decisión | Elección |
|---|---|
| Comportamiento mailto | Descartar enriquecido por completo; plano como ahora |
| Cambio de método | Al cambiar el selector, cambia el editor en el acto |
| Cuerpos | Dos campos separados, **sin migración** |
| Botón de campo dinámico | **Dos botones separados**, uno por editor, cada uno visible solo cuando su editor lo esté |
| Fechas dinámicas | Semana, mes, año y rango custom; sacadas del sistema; opcionales/configurables |

---

## 5. Catálogo de fechas dinámicas del sistema

No provienen de la pauta: se calculan con `DateTime.Now` al momento de generar el correo o PDF.

| Token | Significado | Ejemplo (hoy 09/10/2026, viernes) |
|---|---|---|
| `[Fecha]` | Ya existe. Fecha del registro | `09/10/2026 14:30` |
| `[Hoy]` | Fecha actual del sistema | `09/10/2026` |
| `[Semana]` | Rango lun-dom de la semana actual | `05/10/2026 al 11/10/2026` |
| `[Mes]` | Mes actual, primero a último día | `01/10/2026 al 31/10/2026` |
| `[Año]` | Año actual, primero a último día | `01/01/2026 al 31/12/2026` |
| `[Rango]` | Rango custom configurado | `03/10/2026 al 09/10/2026` |

### Rango custom: modos

| Modo | Definición | Ejemplo |
|---|---|---|
| `Week` | Ventana de N días terminando hoy | `Últimos 7 días` |
| `Fixed` | Desde una fecha hasta otra fecha | `03/10/2026 al 09/10/2026` |
| `Weekday` | Del día A de la semana al día B | `Sábado a viernes` |

Para `Weekday` hay que decidir la convención de `DayOfWeek` y qué pasa cuando el rango cruza el
fin de semana. **Pendiente de definir en implementación** (ver §9).

### Formato configurable

El usuario elige el formato de salida entre:

| Formato | Resultado |
|---|---|
| `dd/MM/yyyy al dd/MM/yyyy` | `03/10/2026 al 09/10/2026` |
| `dd/MM/yyyy - dd/MM/yyyy` | `03/10/2026 - 09/10/2026` |
| `dd/MM/yyyy` (solo fin) | `09/10/2026` |
| `d "de" MMMM "de" yyyy` | `3 de octubre de 2026` |


---

## 6. Arquitectura

### Nuevos archivos

```
Services/DateTokenService.cs            # Cálculo de semana/mes/año/rango
Models/DynamicDateConfig.cs             # Config de rango custom
Views/HtmlEditor/editor.html            # EmbeddedResource, contenteditable
Views/HtmlEditor/HtmlBridge.cs          # [ComVisible] para WebView2
Views/HtmlEditor/RichHtmlEditor.xaml    # + .cs
Views/FieldPickerPopup.xaml             # + .cs
Views/DateTokenPickerPopup.xaml         # + .cs
Views/Converters/EnumEqualsConverter.cs
```

### Modificados

```
PautaDinamicaApp.csproj                     # + Microsoft.Web.WebView2
Models/PautaSchema.cs                       # + EmailBodyHtmlTemplate
Models/ConditionalEmailRule.cs              # + BodyHtmlTemplate
Services/EmailService.cs                    # selección de cuerpo, HtmlEncode, cid
Views/SettingsWindow.xaml                   # editores + botones
Views/SettingsWindow.xaml.cs                # flush del editor
ViewModels/SettingsViewModel.cs             # comandos picker, validación
ViewModels/MainViewModel.cs                 # copia de campo, GetPdfFileName
ViewModels/EditorViewModel.cs               # copia de campo
```

---

## 7. Lista de tareas

Orden de ejecución. Las marcadas ⚠️ corrigen bugs que existen hoy.

### Fase 1 — Bugs latentes (sin UI, verificable por compilación)

| # | Archivo | Tarea |
|---|---|---|
| 1 | `Services/EmailService.cs:16-63` | ⚠️ `HtmlEncode` de valores inyectados. Hoy `García & Hijos <S.A.>` rompe el HTML y Outlook muestra cuerpo vacío. Codificar **solo el valor**, nunca la plantilla |
| 2 | `Services/EmailService.cs:99` | Elegir cuerpo: `EmailBodyHtmlTemplate` si Outlook, `EmailBodyTemplate` si no |
| 3 | `Services/EmailService.cs:186` | Ídem para `SendConditionalEmail` |

### Fase 2 — Modelos

| # | Archivo | Tarea |
|---|---|---|
| 4 | `Models/PautaSchema.cs:95` | Añadir `EmailBodyHtmlTemplate` con `SetProperty` |
| 5 | `Models/ConditionalEmailRule.cs:57` | Añadir `BodyHtmlTemplate` |
| 6 | `Models/DynamicDateConfig.cs` (nuevo) | `Mode` (`Week`/`Fixed`/`Weekday`), `FromDay`, `ToDay`, `FromDate`, `ToDate`, `Format` |
| 7 | `Models/PautaSchema.cs` | Añadir `DynamicDateConfig DynamicDates` + `bool UseDynamicDates` (opcional, default `false`) |

### Fase 3 — Servicio de fechas

| # | Archivo | Tarea |
|---|---|---|
| 8 | `Services/DateTokenService.cs` (nuevo) | `Semana()`, `Mes()`, `Año()`, `Hoy()`, `Rango(DynamicDateConfig)`, `Format(Range, string)` |
| 9 | `Services/EmailService.cs:23` | Ampliar `ProcessTemplate`: resolver `[Semana]`, `[Mes]`, `[Año]`, `[Hoy]`, `[Rango]`. Debe aceptar una instancia de `DateTokenService` inyectada (no `DateTime.Now` hardcodeado, para poder testear) |

### Fase 4 — Editor aislado

| # | Archivo | Tarea |
|---|---|---|
| 10 | `PautaDinamicaApp.csproj` | `Microsoft.Web.WebView2` 1.0.4258.31 |
| 11 | `Views/HtmlEditor/editor.html` | `contenteditable`, toolbar con `execCommand` (bold, italic, underline, color, listas, alineación, enlace, undo, redo, removeFormat), tabla con estilos **inline**, imagen, sanitizado de `paste` |

### Fase 5 — Selectores

| # | Archivo | Tarea |
|---|---|---|
| 14 | `Views/FieldPickerPopup.xaml` (nuevo) | `ListBox` + `TextBox` de filtro, listo para teclear |
| 15 | `Views/DateTokenPickerPopup.xaml` (nuevo) | Lista de tokens + config del rango custom |
| 16 | `Views/Converters/EnumEqualsConverter.cs` (nuevo) | Para `ConverterParameter` con enum |
| 17 | `ViewModels/SettingsViewModel.cs` | `InsertDynamicFieldCommand`, `InsertDynamicDateCommand`, `PickerFields`, `PickerFilter` |

### Fase 6 — Integración UI

| # | Archivo | Tarea |
|---|---|---|
| 18 | `Views/SettingsWindow.xaml:396-413` | `TextBox` y editor como hermanos con `DataTrigger` inverso sobre `SelectedPauta.EmailMethod` |
| 19 | `Views/SettingsWindow.xaml:413` | Botón "Agregar campo dinámico" para mailto |
| 20 | `Views/SettingsWindow.xaml:413` | Botón "Agregar fecha dinámica" para mailto |
| 21 | `Views/HtmlEditor/editor.html` | Ambos botones en la toolbar del editor HTML |
| 22 | `Views/SettingsWindow.xaml:490` | Mismo tratamiento en el cuerpo de cada `ConditionalEmailRule` |
| 23 | `Views/SettingsWindow.xaml.cs` | ⚠️ **Flush del editor** antes de `SaveCommand`/`ApplyCommand` y al cambiar de tab. Sin esto se pierde lo escrito |

### Fase 7 — Validación y copias de pauta

| # | Archivo | Tarea |
|---|---|---|
| 24 | `ViewModels/SettingsViewModel.cs:772` | `ValidateTemplateString` también sobre `EmailBodyHtmlTemplate` |
| 25 | `ViewModels/SettingsViewModel.cs:792` | Ídem sobre `BodyHtmlTemplate` |
| 26 | `ViewModels/MainViewModel.cs:1538` | ⚠️ Copiar `EmailBodyHtmlTemplate` al cargar pauta. Sin esto se pierde al cargar |
| 27 | `ViewModels/EditorViewModel.cs:1028` | ⚠️ Ídem al duplicar/editar pauta |
| 28 | `ViewModels/MainViewModel.cs:1303-1348` | `GetPdfFileName` acepta tokens de fecha para nombres como `Auditorias_2026-10-03_al_2026-10-09.pdf` |

### Fase 8 — Imágenes inline

| # | Archivo | Tarea |
|---|---|---|
| 29 | `Services/EmailService.cs:335` | Content-ID: decodificar `data:` URIs → temporal → `Attachments.Add` → `PropertyAccessor.SetProperty("http://schemas.microsoft.com/office/2007/outlook/mapi/proptag/0x3712001F", "img1")` → reescribir `src` a `cid:img1` → borrar temporales en `finally` |

---

## 8. Inserción en la posición del clic

**TextBox (mailto):**
```csharp
int pos = EmailBodyTextBox.CaretIndex;
string campo = $"[{etiqueta}]";
EmailBodyTextBox.Text = EmailBodyTextBox.Text.Insert(pos, campo);
EmailBodyTextBox.CaretIndex = pos + campo.Length;
EmailBodyTextBox.Focus();
```

**Editor HTML (Outlook):**
```javascript
document.execCommand('insertText', false, '[Etiqueta]');
```

Respeta el caret del usuario. Si no hay selección activa, hace focus al editor y escribe al inicio.

**Fuente de datos:** `CurrentPautaFields` (`SettingsViewModel.cs:222`), ya poblado con la pauta
actual en `LoadPautaData` (línea 421) y ya excluyendo `FieldType.Separator`. No requiere nada nuevo.

| 12 | `Views/HtmlEditor/HtmlBridge.cs` | `[ComVisible(true)]` para `AddHostObjectToScript` |
| 13 | `Views/HtmlEditor/RichHtmlEditor.xaml.cs` | `Html` bidireccional, `InsertTextAtCaretAsync(string)`, `FlushAsync()`, fallback si no hay runtime WebView2 |

**Punto de control:** ventana de prueba para validar el control antes de integrarlo.

Todo opcional y configurable. Si no se configura nada, el comportamiento actual no cambia.


---

## 9. Preguntas abiertas

| # | Pregunta | Estado |
|---|---|---|
| 1 | `[Fecha]` visible en el selector de campos, o solo campos reales | Pendiente |
| 2 | Convención de `DayOfWeek` para `Weekday` y qué hacer cuando el rango cruza el fin de semana | Pendiente |
| 3 | Reglas condicionales: un editor WebView2 por regla (consume RAM) vs uno compartido con patrón maestro-detalle | Pendiente, se recomienda compartido |
| 4 | Si `UseDynamicDates` está activo, ¿los tokens también aplican al nombre del PDF adjunto? | Pendiente |
| 5 | Formato de fecha por defecto cuando no se configura | Propuesto `dd/MM/yyyy` |

---

## 10. Lo que NO se toca

- `SendViaMailto` (`EmailService.cs:232-268`) — sin cambios
- `IsHtml()` (`EmailService.cs:359-366`) — se mantiene
- `EmailBodyTextBox` con su `SpellCheck` y `Language` — intacto
- `[Fecha]` y su resolución actual en `ProcessTemplate` línea 23 — sigue igual
- Plantillas existentes — `EmailBodyTemplate` conserva su contenido

---

## 11. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| ⚠️ Valores con `&`, `<`, `>` rompen el HTML | Tarea 1 (`HtmlEncode`) |
| ⚠️ Editor sin flush antes de guardar | Tarea 23 (flush explícito) |
| ⚠️ Campo HTML perdido al cargar/duplicar pauta | Tareas 26 y 27 |
| Pegar desde Word/Excel inyecta `<style>`/`class` que Outlook ignora | Listener `paste` que sanea a estilos inline (tarea 11) |
| WebView2 ausente en máquinas antiguas | Detección al cargar + degradar al `TextBox` plano (tarea 13) |
| `execCommand` deprecado en Chromium | Sigue operativo, sin reemplazo equivalente simple |
| Varios WebView2 en reglas condicionales = mucho RAM | Editor compartido maestro-detalle (pregunta 3) |
| `[Fecha]` vs fecha del registro: confusión de significado | Documentar que `[Fecha]` es del registro y `[Hoy]` del sistema |

---

## 12. Entorno verificado

| Item | Valor |
|---|---|
| Runtime WebView2 | `154.0.4258.62` en `C:\Program Files (x86)\Microsoft\EdgeWebView\Application` |
| Paquete NuGet | `Microsoft.Web.WebView2` 1.0.4258.31 |
| SDK | 10.0.401 |
| Target | `net8.0-windows`, `UseWPF` + `UseWindowsForms` |
| Dependencias actuales | Solo `ClosedXML` 0.105.0 y `QuestPDF` 2025.12.3 |
| Rama | `development` |

---

## 13. Registro de avance

| Fecha | Tareas | Notas |
|---|---|---|
| 09/10/2026 | — | Plan redactado, pendiente de ejecución |
| 09/10/2026 | 1, 2, 3, 4, 5, 6, 7, 8, 9 | ✅ Fases 1-3 completadas. Compilación correcta (0 errores). `DateTokenService` validado con 20/20 pruebas en verde |

---

## 14. Notas de implementación (decisiones tomadas al codificar)

### 14.1 Semántica retrospectiva del rango semanal

`VentanaSemanal` devuelve el bloque más reciente que **ya cerró** en `ToDay`, no el que está en
curso. Es lo que necesita una auditoría: reporta el periodo terminado.

Con hoy = viernes 09/10/2026:

| Config | Resultado |
|---|---|
| Sábado → viernes | `03/10/2026 al 09/10/2026` (cierra hoy) |
| Domingo → sábado | `27/09/2026 al 03/10/2026` (el bloque 04/10→10/10 aún no cierra) |

El orden invertido (sábado → viernes) cruza el fin de semana sin problema.

### 14.2 Codificación: dos capas diferenciadas

| Capa | Cuándo codifica | Por qué |
|---|---|---|
| `ProcessTemplate` (valores de campo) | Si `encodeValues == true` | El valor viene del usuario y puede traer `&`, `<`, `>` |
| `InsertDynamicDates` (rangos generados) | Si el cuerpo es HTML | Por higiene: hoy los rangos no traen caracteres peligrosos, pero la regla debe ser uniforme |

En ambos casos se codifica **solo el valor**, nunca la plantilla, para no destruir el HTML que el
usuario escribió en el editor.

El matching de `EmailReplacementRule` sigue usando el valor **crudo**: la codificación se aplica
solo al insertar, justo antes del `Replace`.

### 14.3 Herencia en correos condicionales

El cuerpo HTML de una regla condicional tiene prioridad; si está vacío **hereda** el del correo
principal. Es el mismo criterio que ya existía para `ToTemplate` y `CcTemplate`, así que no
introduce un comportamiento nuevo.

### 14.4 Fecha inyectable para pruebas

`DateTokenService` acepta un `Func<DateTime>` en lugar de usar `DateTime.Now` directamente, lo que
permite pruebas deterministas. `EmailService` se apoya en eso con su constructor
`EmailService(DateTokenService)`.

### 14.5 Robustez de los cálculos

- `Days` se acota a `[1, 3660]`: un `0` o un negativo no revienta ni produce rangos invertidos.
- `Format` normaliza `From`/`To` si llegan invertidos.
- `Fixed` sin fechas capturadas degrada a la ventana de días en vez de lanzar.
- `UseDynamicDates == false` → `ResolveTokens` devuelve el texto intacto, sin ningún costo.

