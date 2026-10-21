using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace DfaSimulator.Core
{
    // DfaSaveData, StateSaveData and TransitionSaveData live in AutomatonEditor.Core (shared with the tests).
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public class StateModel : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private double _x;
        public double X
        {
            get => _x;
            set
            {
                _x = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterX));
            }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set
            {
                _y = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenterY));
            }
        }

        private double _radius = 25;
        public double Radius
        {
            get => _radius;
            set
            {
                _radius = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Diameter));
                OnPropertyChanged(nameof(CenterX));
                OnPropertyChanged(nameof(CenterY));
            }
        }

        public double Diameter => Radius * 2;
        public double CenterX => X + Radius;
        public double CenterY => Y + Radius;

        private string _fillColor = "White";
        public string FillColor { get => _fillColor; set { _fillColor = value; OnPropertyChanged(); } }

        private string _strokeColor = "Black";
        public string StrokeColor { get => _strokeColor; set { _strokeColor = value; OnPropertyChanged(); } }

        private double _strokeThickness = 2;
        public double StrokeThickness { get => _strokeThickness; set { _strokeThickness = value; OnPropertyChanged(); } }

        private bool _isActive;
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

        private bool _isAccepting;
        public bool IsAccepting { get => _isAccepting; set { _isAccepting = value; OnPropertyChanged(); } }

        private bool _isInitial;
        public bool IsInitial
        {
            get => _isInitial;
            set
            {
                if (_isInitial == value) return;
                _isInitial = value;
                OnPropertyChanged();
            }
        }

        private bool _isCurrentSimulationState;
        public bool IsCurrentSimulationState { get => _isCurrentSimulationState; set { _isCurrentSimulationState = value; OnPropertyChanged(); } }
    }

    public class TransitionModel : ObservableObject
    {
        private StateModel _from;
        public StateModel From
        {
            get => _from;
            set
            {
                if (_from != null) _from.PropertyChanged -= State_PropertyChanged;
                _from = value;
                if (_from != null) _from.PropertyChanged += State_PropertyChanged;
                OnPropertyChanged();
                RefreshLine();
            }
        }

        private StateModel _to;
        public StateModel To
        {
            get => _to;
            set
            {
                if (_to != null) _to.PropertyChanged -= State_PropertyChanged;
                _to = value;
                if (_to != null) _to.PropertyChanged += State_PropertyChanged;
                OnPropertyChanged();
                RefreshLine();
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _symbol;
        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(); } }

        private void State_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY")
            {
                RefreshLine();
            }
        }

        public System.Windows.Point StartPoint => From != null ? new System.Windows.Point(From.CenterX, From.CenterY) : new System.Windows.Point();
        public System.Windows.Point EndPoint => To != null ? new System.Windows.Point(To.CenterX, To.CenterY) : new System.Windows.Point();

        public System.Windows.Point ControlPoint
        {
            get
            {
                if (From == null || To == null) return new System.Windows.Point();
                double offsetX = (To.CenterY - From.CenterY) * 0.15;
                double offsetY = (To.CenterX - From.CenterX) * 0.15;
                return new System.Windows.Point((From.CenterX + To.CenterX) / 2 - offsetX, (From.CenterY + To.CenterY) / 2 + offsetY);
            }
        }

        public double MidX => IsSelfLoop ? From.CenterX : (StartPoint.X + 2 * ControlPoint.X + EndPoint.X) / 4;
        public double MidY => IsSelfLoop ? From.Y - 20 : (StartPoint.Y + 2 * ControlPoint.Y + EndPoint.Y) / 4;

        public double Angle
        {
            get
            {
                if (From == null || To == null) return 0;
                return Math.Atan2(EndPoint.Y - StartPoint.Y, EndPoint.X - StartPoint.X) * (180 / Math.PI);
            }
        }

        private void RefreshLine()
        {
            OnPropertyChanged(nameof(StartPoint));
            OnPropertyChanged(nameof(EndPoint));
            OnPropertyChanged(nameof(ControlPoint));
            OnPropertyChanged(nameof(MidX));
            OnPropertyChanged(nameof(MidY));
            OnPropertyChanged(nameof(Angle));
        }

        public bool IsSelfLoop => From == To;
        public bool IsNotSelfLoop => From != To;
    }

    public class OffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val && parameter != null)
            {
                if (double.TryParse(parameter.ToString(), out double offset))
                {
                    return val + offset;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CharDisplayModel : ObservableObject
    {
        private char _character;
        public char Character
        {
            get => _character;
            set { _character = value; OnPropertyChanged(); }
        }

        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set { _isCurrent = value; OnPropertyChanged(); }
        }
    }
}