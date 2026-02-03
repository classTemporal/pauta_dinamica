using System.Windows;
using System.Windows.Controls;
using PautaDinamicaApp.Models;

namespace PautaDinamicaApp.Views
{
    public class FieldDefaultValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? DropdownTemplate { get; set; }
        public DataTemplate? TimeTemplate { get; set; }
        public DataTemplate? DateTemplate { get; set; }
        public DataTemplate? EmptyTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is FieldDefinition field)
            {
                switch (field.Type)
                {
                    case FieldType.Text:
                    case FieldType.Numeric:
                    case FieldType.TextArea:
                        return TextTemplate;
                    case FieldType.Boolean:
                        return BooleanTemplate;
                    case FieldType.Dropdown:
                        return DropdownTemplate;
                    case FieldType.Time:
                        return TimeTemplate;
                    case FieldType.Date:
                        return DateTemplate;
                    default:
                        return EmptyTemplate;
                }
            }
            return base.SelectTemplate(item, container);
        }
    }
}
