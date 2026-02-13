namespace PautaDinamicaApp.ViewModels
{
    public class SelectableFieldViewModel : ViewModelBase
    {
        private bool _isSelected;
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
