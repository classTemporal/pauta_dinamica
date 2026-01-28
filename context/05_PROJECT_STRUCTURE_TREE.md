# DIRECTORY TREE (WPF .NET 8)

/Root
├── App.xaml / App.xaml.cs    # Punto de entrada
├── MainProject.csproj        # Configuración de compilación Single-File
│
├── /Models
│   ├── FieldDefinition.cs    # Metadata de pautas
│   └── AuditEntry.cs         # Datos recolectados
│
├── /ViewModels
│   ├── MainViewModel.cs      # Orquestador
│   └── DynamicFieldVM.cs     # ViewModel para cada input dinámico
│
├── /Services
│   ├── ExcelService.cs       # Lógica de ClosedXML
│   ├── StorageService.cs     # Serialización JSON
│   └── MailService.cs        # Formateador de Mailto
│
└── /Views
    ├── MainWindow.xaml       # Ventana principal
    └── /Resources
        └── FieldTemplates.xaml # Definición de controles (TextBox, CheckBox)