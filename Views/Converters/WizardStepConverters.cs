using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PautaDinamicaApp.Views.Converters
{
    /// <summary>
    /// Converts an int step index to the Visibility for Step 0 (fields configuration).
    /// Returns Visible when value == 0, Collapsed otherwise.
    /// </summary>
    public class IntToStep0VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(value ?? 0) == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an int step index to the Visibility for Step 1 (dashboard ordering).
    /// Returns Visible when value == 1, Collapsed otherwise.
    /// </summary>
    public class IntToStep1VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(value ?? 0) == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an int step index to the Visibility for Step 2 (email configuration).
    /// Returns Visible when value == 2, Collapsed otherwise.
    /// </summary>
    public class IntToStep2VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(value ?? 0) == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns true when CurrentStep > 0 (i.e., the "Previous" button should be enabled).
    /// </summary>
    public class IntToHasPreviousStepConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(value ?? 0) > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns the "Next" or "Finish" button text based on whether we are at the last step.
    /// This is intended for binding on the wizard ViewModel, but also works as a fallback converter.
    /// </summary>
    public class IntToNextOrFinishTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(value ?? 0) >= 2 ? "Finalizar" : "Siguiente";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns whether the Next/Finish button can be enabled based on step index.
    /// This converter returns true for all steps — the actual logic is handled by the ViewModel's CanExecute.
    /// Used as a fallback binding converter for the button's IsEnabled.
    /// </summary>
    public class CanMoveToNextStepConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Always enabled; actual validation is handled by the command's CanExecute
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
