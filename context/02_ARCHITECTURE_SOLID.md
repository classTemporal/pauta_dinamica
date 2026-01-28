# LOGICAL ARCHITECTURE (SOLID & CLEAN CODE)

## 1. MODELS (Capa de Datos)
- `FieldDefinition`: Objeto que describe un campo (ID, Etiqueta, Tipo de Dato, Validación).
- `AuditRecord`: Diccionario de respuestas asociado a una marca de tiempo.

## 2. VIEWMODELS (Capa de Conexión)
- `MainViewModel`: Gestor de la colección de registros y comandos de usuario.
- `EditorViewModel`: Lógica para la creación y edición de la estructura de la auditoría.

## 3. SERVICES (Capa de Negocio)
- `ExcelExportService`: Transformación de datos a Excel con filtrado de columnas.
- `MailService`: Orquestador de URI mailto para comunicación externa.
- `ConfigProvider`: Persistencia de la estructura dinámica en archivos locales.

## 4. UI (Capa de Presentación)
- `FieldTemplateSelector`: Clase que selecciona el DataTemplate (TextBox, CheckBox, etc.) basándose en el tipo definido en el modelo.