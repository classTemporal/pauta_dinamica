# ROLE: Senior .NET Software Architect
# PROJECT: Dynamic Audit App (Native Windows)
# STACK: C# / .NET 8 / WPF (Windows Presentation Foundation)
# DEPLOYMENT: Single-File Executable (Self-Contained)

## OBJETIVO CORE
Desarrollar una aplicación de escritorio nativa para Windows que permita la creación y gestión dinámica de auditorías. La aplicación debe ser un archivo único (.exe) que no dependa de instalaciones externas de Python o runtimes compartidos.

## RESTRICCIONES TÉCNICAS
1. **Compilación Single-File**: Configurar para que todas las DLLs se incluyan en el ejecutable.
2. **Patrón MVVM**: Separación total entre la lógica de datos y la interfaz de usuario.
3. **Dinamismo**: La interfaz no se diseña campo por campo; se genera mapeando un JSON de configuración a controles de Windows.
4. **Excel**: Generación nativa de .xlsx mediante librerías como ClosedXML.