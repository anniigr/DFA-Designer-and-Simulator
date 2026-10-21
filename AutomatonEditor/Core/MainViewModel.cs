using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AutomatonEditor.Core;

namespace DfaSimulator.Core
{
    public class MainViewModel : ObservableObject
    {
        private int _stateCounter = 0;

        public ObservableCollection<StateModel> States { get; } = new ObservableCollection<StateModel>();
        public ObservableCollection<TransitionModel> Transitions { get; } = new ObservableCollection<TransitionModel>();

        private System.Collections.Generic.Stack<StateModel> _simulationHistory = new System.Collections.Generic.Stack<StateModel>();

        public ObservableCollection<string> SimulationHistorySteps { get; } = new ObservableCollection<string>();

        public ObservableCollection<CharDisplayModel> InputWordCharacters { get; } = new ObservableCollection<CharDisplayModel>();

        private StateModel _selectedState;
        public StateModel SelectedState
        {
            get => _selectedState;
            set
            {
                if (_selectedState != null) _selectedState.IsActive = false;

                _selectedState = value;

                if (_selectedState != null)
                {
                    _selectedState.IsActive = true;
                    SelectedTransition = null;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStateSelected));

                DeleteTransitionCommand?.RaiseCanExecuteChanged();
            }
        }

        public bool IsStateSelected => SelectedState != null;

        public bool CanEditInput => !IsAnimating && CurrentSimulationIndex == 0;

        private StateModel _newTransitionFrom;
        public StateModel NewTransitionFrom
        {
            get => _newTransitionFrom;
            set { _newTransitionFrom = value; OnPropertyChanged(); }
        }

        private StateModel _newTransitionTo;
        public StateModel NewTransitionTo
        {
            get => _newTransitionTo;
            set { _newTransitionTo = value; OnPropertyChanged(); }
        }

        private string _newTransitionSymbol;
        public string NewTransitionSymbol
        {
            get => _newTransitionSymbol;
            set { _newTransitionSymbol = value; OnPropertyChanged(); }
        }

        private TransitionModel _selectedTransition;
        public TransitionModel SelectedTransition
        {
            get => _selectedTransition;
            set
            {
                if (_selectedTransition != null) _selectedTransition.IsActive = false;

                _selectedTransition = value;

                if (_selectedTransition != null)
                {
                    _selectedTransition.IsActive = true;
                    SelectedState = null;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTransitionSelected));
            }
        }

        public bool IsTransitionSelected => SelectedTransition != null;

        private string _inputWord;
        public string InputWord
        {
            get => _inputWord;
            set
            {
                if (_inputWord != value)
                {
                    if (string.IsNullOrEmpty(value) || IsWordValid(value))
                    {
                        _inputWord = value;

                        InputWordCharacters.Clear();

                        if (!string.IsNullOrEmpty(_inputWord))
                        {
                            foreach (char c in _inputWord)
                            {
                                InputWordCharacters.Add(new CharDisplayModel { Character = c, IsCurrent = false });
                            }
                        }

                        OnPropertyChanged();
                        ResetSimulation();
                    }
                    else
                    {
                        MessageBox.Show("Słowo zawiera symbole spoza alfabetu automatu!",
                                        "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private bool IsWordValid(string word)
        {
            var validSymbols = Transitions
                .SelectMany(t => t.Symbol.Split(','))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            return word.All(c => validSymbols.Contains(c.ToString()));
        }

        public string SimulationStatus { get; set; } = "Gotowy";

        private int _currentSimulationIndex = 0;

        private StateModel _currentSimulationState;

        private DispatcherTimer _animationTimer;

        private bool _isAnimating;

        private double _animationSpeed = 1000;

        public bool IsAnimating
        {
            get => _isAnimating;
            set
            {
                _isAnimating = value;
                OnPropertyChanged();

                StartAnimationCommand?.RaiseCanExecuteChanged();
                StopAnimationCommand?.RaiseCanExecuteChanged();
                SimulateStepCommand?.RaiseCanExecuteChanged();
                SimulatePreviousStepCommand?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditInput));
            }
        }

        public double AnimationSpeed
        {
            get => _animationSpeed;
            set
            {
                _animationSpeed = value;
                OnPropertyChanged();

                if (_animationTimer != null)
                {
                    _animationTimer.Interval = TimeSpan.FromMilliseconds(_animationSpeed);
                }
            }
        }

        public RelayCommand StartAnimationCommand { get; }
        public RelayCommand StopAnimationCommand { get; }
        public RelayCommand DeleteStateCommand { get; }
        public RelayCommand AddTransitionCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand LoadCommand { get; }
        public RelayCommand SimulateStepCommand { get; }
        public RelayCommand ResetSimulationCommand { get; }
        public RelayCommand DeleteTransitionCommand { get; }
        public RelayCommand SimulatePreviousStepCommand { get; }

        public int CurrentSimulationIndex
        {
            get => _currentSimulationIndex;
            set
            {
                _currentSimulationIndex = value;
                OnPropertyChanged();

                SimulateStepCommand?.RaiseCanExecuteChanged();
                SimulatePreviousStepCommand?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditInput));
            }
        }

        private void DeleteTransition()
        {
            if (SelectedTransition == null)
            {
                MessageBox.Show("Najpierw kliknij na etykietę przejścia (np. '0,1') na холсте, aby je zaznaczyć, a następnie kliknij Usuń.");
                return;
            }

            Transitions.Remove(SelectedTransition);
            SelectedTransition = null;

            OnPropertyChanged(nameof(Alphabet));
        }

        public string Alphabet => string.Join(", ", Transitions
        .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
        .SelectMany(t => t.Symbol.Split(','))
        .Select(s => s.Trim())
        .Distinct()
        .OrderBy(s => s));

        public MainViewModel()
        {
            DeleteStateCommand = new RelayCommand(_ => DeleteState(), _ => IsStateSelected);
            AddTransitionCommand = new RelayCommand(_ => AddTransition());
            SaveCommand = new RelayCommand(_ => SaveDfa());
            LoadCommand = new RelayCommand(_ => LoadDfa());

            SimulateStepCommand = new RelayCommand(
                _ => SimulateStep(),
                _ => !IsAnimating && CanSimulateStep()
            );

            SimulatePreviousStepCommand = new RelayCommand(_ => SimulatePreviousStep(), _ => CanSimulatePrevious());

            ResetSimulationCommand = new RelayCommand(_ => ResetSimulation(), _ => States.Count > 0);

            StartAnimationCommand = new RelayCommand(_ => StartAnimation(), _ => CanStartAnimation());
            StopAnimationCommand = new RelayCommand(_ => StopAnimation(), _ => IsAnimating);

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(AnimationSpeed);
            _animationTimer.Tick += AnimationTimer_Tick;

            DeleteTransitionCommand = new RelayCommand(_ => DeleteTransition(), _ => IsTransitionSelected);
        }

        public void AddNewState(double x, double y)
        {
            var newState = new StateModel
            {
                Name = $"q{_stateCounter++}",
                X = x - 25,
                Y = y - 25,
                IsInitial = States.Count == 0
            };

            newState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StateModel.IsInitial))
                {
                    if (newState.IsInitial)
                    {
                        foreach (var state in States.Where(st => st != newState))
                        {
                            state.IsInitial = false;
                        }
                    }
                    SimulateStepCommand?.RaiseCanExecuteChanged();
                    StartAnimationCommand?.RaiseCanExecuteChanged();
                }
            };

            States.Add(newState);
        }

        private void DeleteState()
        {
            if (SelectedState == null) return;

            var toRemove = Transitions.Where(t => t.From == SelectedState || t.To == SelectedState).ToList();

            foreach (var t in toRemove) Transitions.Remove(t);

            States.Remove(SelectedState);
            SelectedState = null;
        }

        private void AddTransition()
        {
            if (NewTransitionFrom == null || NewTransitionTo == null || string.IsNullOrWhiteSpace(NewTransitionSymbol))
            {
                MessageBox.Show("Wypełnij wszystkie pola przejścia.");
                return;
            }

            var newSymbols = NewTransitionSymbol.Split(',').Select(s => s.Trim()).ToList();

            if (newSymbols.Any(s => s.Length != 1))
            {
                MessageBox.Show("Symbole przejścia muszą być pojedynczymi znakami (np. '0' lub '0,1'). Symbole typu '0000' są niepoprawne.",
                                "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var sym in newSymbols)
            {
                if (Transitions.Any(t => t.From == NewTransitionFrom && t.Symbol.Split(',').Select(s => s.Trim()).Contains(sym)))
                {
                    MessageBox.Show($"Stan {NewTransitionFrom.Name} ma już przejście dla symbolu '{sym}'.");
                    return;
                }
            }

            Transitions.Add(new TransitionModel
            {
                From = NewTransitionFrom,
                To = NewTransitionTo,
                Symbol = NewTransitionSymbol
            });

            OnPropertyChanged(nameof(Alphabet));
        }

        private void SaveDfa()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = "json",
                FileName = "automat"
            };

            if (dialog.ShowDialog() == true)
            {
                var data = new DfaSaveData
                {
                    States = States.Select(s => new StateSaveData
                    {
                        Id = s.Id,
                        Name = s.Name,
                        X = s.X,
                        Y = s.Y,
                        IsInitial = s.IsInitial,
                        IsAccepting = s.IsAccepting
                    }).ToArray(),
                    Transitions = Transitions.Select(t => new TransitionSaveData
                    {
                        FromId = t.From.Id,
                        ToId = t.To.Id,
                        Symbol = t.Symbol
                    }).ToArray()
                };

                File.WriteAllText(dialog.FileName, DfaJson.Serialize(data));
                MessageBox.Show("Zapisano pomyślnie!");
            }
        }

        private void LoadDfa()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Wybierz plik z automatem",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples")
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var data = DfaJson.TryDeserialize(json, out var parseError);

                    if (data == null)
                    {
                        MessageBox.Show($"Błąd: Plik nie jest poprawnym plikiem JSON.\nSzczegóły: {parseError}",
                                        "Błąd parsowania", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // The same rules the editor enforces interactively (one initial state, single-character
                    // symbols, determinism, no dangling transitions) are re-checked for files, which may be hand-edited.
                    var problems = DfaValidator.Validate(data);
                    if (problems.Count > 0)
                    {
                        MessageBox.Show("Plik nie opisuje poprawnego automatu DFA:\n\n - " +
                                        string.Join("\n - ", problems.Select(p => p.Message)),
                                        "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    States.Clear();
                    Transitions.Clear();
                    SelectedState = null;
                    SelectedTransition = null;
                    _currentSimulationState = null;

                    int maxId = 0;

                    foreach (var s in data.States)
                    {
                        if (string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Name))
                        {
                            throw new Exception("W pliku znajduje się stan bez przypisanego Id lub Name.");
                        }

                        var state = new StateModel
                        {
                            Id = s.Id,
                            Name = s.Name,
                            X = s.X,
                            Y = s.Y,
                            IsInitial = s.IsInitial,
                            IsAccepting = s.IsAccepting
                        };

                        state.PropertyChanged += (sender, e) =>
                        {
                            if (e.PropertyName == nameof(StateModel.IsInitial))
                            {
                                if (state.IsInitial)
                                {
                                    foreach (var st in States.Where(x => x != state)) st.IsInitial = false;
                                }

                                SimulateStepCommand?.RaiseCanExecuteChanged();
                                StartAnimationCommand?.RaiseCanExecuteChanged();
                            }
                        };

                        States.Add(state);

                        if (s.Name.StartsWith("q") && int.TryParse(s.Name.Substring(1), out int num))
                        {
                            if (num >= maxId) maxId = num + 1;
                        }
                    }

                    _stateCounter = maxId;

                    foreach (var t in data.Transitions)
                    {
                        var fromState = States.FirstOrDefault(x => x.Id == t.FromId);
                        var toState = States.FirstOrDefault(x => x.Id == t.ToId);

                        if (fromState == null || toState == null)
                        {
                            throw new Exception($"Nie znaleziono stanu dla przejścia (Z: {t.FromId}, Do: {t.ToId}). Plik jest uszkodzony.");
                        }

                        Transitions.Add(new TransitionModel
                        {
                            From = fromState,
                            To = toState,
                            Symbol = t.Symbol
                        });
                    }

                    OnPropertyChanged(nameof(Alphabet));
                    MessageBox.Show("Automat wczytany poprawnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas walidacji pliku: {ex.Message}",
                                    "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Error);

                    States.Clear();
                    Transitions.Clear();
                }
            }
        }

        private void ResetSimulation()
        {
            StopAnimation();

            foreach (var s in States) s.IsCurrentSimulationState = false;

            var initial = States.FirstOrDefault(s => s.IsInitial);
            if (initial == null)
            {
                SimulationStatus = "Błąd: Brak stanu początkowego!";
                return;
            }

            _currentSimulationState = initial;
            _currentSimulationState.IsCurrentSimulationState = true;

            _currentSimulationIndex = 0;
            SimulationStatus = "Symulacja zresetowana. Gotowy.";
            OnPropertyChanged(nameof(SimulationStatus));

            _simulationHistory.Clear();
            SimulationHistorySteps.Clear();

            foreach (var c in InputWordCharacters) c.IsCurrent = false;

            foreach (var t in Transitions) t.IsActive = false;
        }

        private void SimulateStep()
        {
            if (CurrentSimulationIndex == 0 || _currentSimulationState == null)
            {
                foreach (var s in States) s.IsCurrentSimulationState = false;
                var initial = States.FirstOrDefault(s => s.IsInitial);
                if (initial == null)
                {
                    SimulationStatus = "Błąd: Brak stanu początkowego!";
                    OnPropertyChanged(nameof(SimulationStatus));
                    return;
                }
                _currentSimulationState = initial;
                _currentSimulationState.IsCurrentSimulationState = true;
                _simulationHistory.Clear();
                SimulationHistorySteps.Clear();
                CurrentSimulationIndex = 0;
            }

            if (_currentSimulationState == null) return;

            if (string.IsNullOrEmpty(InputWord) || CurrentSimulationIndex >= InputWord.Length) return;

            for (int i = 0; i < InputWordCharacters.Count; i++)
            {
                InputWordCharacters[i].IsCurrent = (i == CurrentSimulationIndex);
            }

            char currentSymbol = InputWord[CurrentSimulationIndex];

            var nextTransition = Transitions.FirstOrDefault(t => t.From == _currentSimulationState &&
                t.Symbol.Split(',').Select(s => s.Trim()).Contains(currentSymbol.ToString()));

            if (nextTransition == null)
            {
                SimulationStatus = $"BŁĄD/ODRZUCONO: brak przejścia dla symbolu '{currentSymbol}' ze stanu '{_currentSimulationState.Name}'.";
                OnPropertyChanged(nameof(SimulationStatus));
                CurrentSimulationIndex = InputWord.Length;
                StopAnimation();
                foreach (var t in Transitions) t.IsActive = false;
                return;
            }

            foreach (var t in Transitions) t.IsActive = false;
            nextTransition.IsActive = true;
            SimulationHistorySteps.Add($"{_currentSimulationState.Name} --({currentSymbol})--> {nextTransition.To.Name}");
            _simulationHistory.Push(_currentSimulationState);

            _currentSimulationState.IsCurrentSimulationState = false;
            _currentSimulationState = nextTransition.To;
            _currentSimulationState.IsCurrentSimulationState = true;

            CurrentSimulationIndex++;

            if (CurrentSimulationIndex >= InputWord.Length)
            {
                if (_currentSimulationState.IsAccepting)
                {
                    SimulationStatus = "ZAKOŃCZONO: słowo ZAAKCEPTOWANE! wszystko prawidłowo.";
                }
                else
                {
                    SimulationStatus = "ZAKOŃCZONO: słowo ODRZUCONE. stan nie jest akceptujący.";
                }

                StopAnimation();
                foreach (var t in Transitions) t.IsActive = false;
            }
            else
            {
                SimulationStatus = $"krok {CurrentSimulationIndex}: przejście do {_currentSimulationState.Name} przez symbol '{currentSymbol}'.";
            }

            OnPropertyChanged(nameof(SimulationStatus));
        }

        private void SimulatePreviousStep()
        {
            if (_simulationHistory.Count == 0 || CurrentSimulationIndex <= 0)
                return;

            _currentSimulationState.IsCurrentSimulationState = false;

            _currentSimulationState = _simulationHistory.Pop();
            _currentSimulationState.IsCurrentSimulationState = true;

            CurrentSimulationIndex--;

            SimulationStatus = $"Cofnięto do kroku {CurrentSimulationIndex}: Aktualny stan to {_currentSimulationState.Name}.";
            OnPropertyChanged(nameof(SimulationStatus));

            if (SimulationHistorySteps.Count > 0)
                SimulationHistorySteps.RemoveAt(SimulationHistorySteps.Count - 1);

            foreach (var t in Transitions) t.IsActive = false;

            for (int i = 0; i < InputWordCharacters.Count; i++)
            {
                InputWordCharacters[i].IsCurrent = (i == CurrentSimulationIndex - 1);
            }
        }

        private void StartAnimation()
        {
            IsAnimating = true;
            _animationTimer.Start();
        }

        public void StopAnimation()
        {
            IsAnimating = false;
            _animationTimer.Stop();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (CanSimulateStep())
            {
                SimulateStep();
            }
            else
            {
                StopAnimation();
            }
        }

        private bool CanStartAnimation()
        {
            return !IsAnimating && CanSimulateStep();
        }

        private bool CanSimulateStep()
        {
            if (CurrentSimulationIndex == 0)
            {
                return !string.IsNullOrEmpty(InputWord) && States.Any(s => s.IsInitial);
            }

            return !string.IsNullOrEmpty(InputWord) &&
                   CurrentSimulationIndex < InputWord.Length &&
                   _currentSimulationState != null;
        }

        private bool CanSimulatePrevious()
        {
            return !IsAnimating && CurrentSimulationIndex > 0 && _simulationHistory.Count > 0;
        }
    }
}