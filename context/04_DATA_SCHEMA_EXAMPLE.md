# DATA STRUCTURES (JSON)

## ESQUEMA DE CONFIGURACIÓN (Estructura de la App)
[
  {
    "Id": "f_01",
    "Label": "Reporte No.",
    "Type": "Numeric",
    "IsRequired": true
  },
  {
    "Id": "f_02",
    "Label": "Estado de Auditoría",
    "Type": "Dropdown",
    "Options": ["Pasa", "No Pasa", "N/A"],
    "IsRequired": true
  }
]

## ESQUEMA DE REGISTROS (Datos Guardados)
[
  {
    "RecordId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "Values": {
      "f_01": 10254,
      "f_02": "Pasa"
    }
  }
]