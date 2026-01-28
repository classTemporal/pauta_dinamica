# REQUERIMIENTOS FUNCIONALES DETALLADOS

## A. MOTOR DE FORMULARIOS DINÁMICOS
El usuario debe poder:
- Definir campos nuevos (ej. "Ticket ID", "Cumplimiento").
- Elegir tipos de entrada: Texto, Numérico, Fecha, Booleano.
- Establecer reglas de validación (Obligatorio/Opcional).

## B. GESTIÓN DE DATOS Y EXPORTACIÓN
- Visualización en un DataGrid que ajusta sus columnas automáticamente.
- Exportación selectiva: Un diálogo debe permitir marcar qué campos se incluirán en el reporte final de Excel.
- CRUD: Capacidad de editar o eliminar registros de la lista antes de exportar.

## C. INTEGRACIÓN DE CORREO
- Generar cuerpo de mensaje automático mediante mailto, concatenando los valores de la fila seleccionada por el usuario.