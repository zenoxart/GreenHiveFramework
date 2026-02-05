using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Zenox.NET.Core.Common.Tutorial
{
    /// <summary>
    /// Verwaltet das Verhalten des Tutorials
    /// </summary>
    public class TutorialManager
    {
        private readonly System.Windows.Window _window;
        private readonly Canvas _overlay;
        private readonly TutorialPopup _popup;
        private int _currentIndex = -1;
        private DispatcherTimer? _completionTimer;

        /// <summary>
        /// Stellt eine Auflistung an Tutorialschritten
        /// </summary>
        public List<TutorialStep> Steps { get; } = new();
        public bool IsActive { get; private set; }

        public event EventHandler<TutorialStepEventArgs>? StepChanged;
        public event EventHandler? TutorialCompleted;
        public event EventHandler? TutorialCancelled;

        public TutorialManager(System.Windows.Window window, Canvas overlay)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _popup = new TutorialPopup();

            _popup.NextClicked += OnNextClicked;
            _popup.PreviousClicked += OnPreviousClicked;
            _popup.SkipClicked += OnSkipClicked;
            _popup.CloseClicked += OnCloseClicked;
        }

        public void Start()
        {
            if (Steps.Count == 0) return;

            IsActive = true;
            _currentIndex = -1;
            _overlay.Visibility = System.Windows.Visibility.Visible;
            ShowNextStep();
        }

        public void ShowNextStep()
        {
            _currentIndex++;
            if (_currentIndex >= Steps.Count)
            {
                CompleteTutorial();
                return;
            }

            ShowCurrentStep();
        }

        public void ShowPreviousStep()
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                ShowCurrentStep();
            }
        }

        private void ShowCurrentStep()
        {
            if (_currentIndex < 0 || _currentIndex >= Steps.Count) return;

            var step = Steps[_currentIndex];
            var element = FindElementByTutorialKey(_window, step.Key);

            if (element != null)
            {
                var bounds = GetElementBounds(element);
                ShowOverlay(bounds, step);

                // Auto-completion check
                if (step.CompletionCondition != null)
                {
                    StartCompletionTimer(step.CompletionCondition);
                }
            }
            else
            {
                // Element nicht gefunden, nächsten Schritt versuchen
                ShowNextStep();
            }

            StepChanged?.Invoke(this, new TutorialStepEventArgs(step, _currentIndex, Steps.Count));
        }

        /// <summary>
        /// Zeichnet das Overlay
        /// </summary>
        /// <param name="targetBounds">Umriss des hervorzuhebenden Elements</param>
        /// <param name="step"></param>
        private void ShowOverlay(System.Windows.Rect targetBounds, TutorialStep step)
        {
            // Overlay mit Ausschnitt für das Target-Element erstellen
            _overlay.Children.Clear();

            var overlayGeometry = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(new System.Windows.Rect(0, 0, _overlay.ActualWidth, _overlay.ActualHeight)),
                new RectangleGeometry(targetBounds)
            );

            var overlayPath = new System.Windows.Shapes.Path
            {
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 0)),
                Data = overlayGeometry
            };

            _overlay.Children.Add(overlayPath);

            // Popup positionieren
            PositionPopup(targetBounds, step);
        }

        /// <summary>
        /// Positioniert das Helper-Popup
        /// </summary>
        private void PositionPopup(System.Windows.Rect targetBounds, TutorialStep step)
        {
            _popup.UpdateContent(step.Title, step.InstructionText,
                _currentIndex + 1, Steps.Count,
                _currentIndex > 0, step.CanSkip);

            // Sicherstellen, dass die Popup-Größe aktualisiert wurde
            _popup.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            _popup.Arrange(new System.Windows.Rect(_popup.DesiredSize));

            var popupWidth = _popup.DesiredSize.Width;
            var popupHeight = _popup.DesiredSize.Height;

            // Margins für besseren Abstand
            const double margin = 10;
            const double edgeMargin = 20; // Abstand zu den Bildschirmrändern

            System.Windows.Point popupPosition = step.Position switch
            {
                TutorialPosition.Top => CalculateTopPosition(targetBounds, popupWidth, popupHeight, margin),
                TutorialPosition.Bottom => CalculateBottomPosition(targetBounds, popupWidth, popupHeight, margin),
                TutorialPosition.Left => CalculateLeftPosition(targetBounds, popupWidth, popupHeight, margin),
                TutorialPosition.Right => CalculateRightPosition(targetBounds, popupWidth, popupHeight, margin),
                TutorialPosition.Center => new System.Windows.Point(
                    (_overlay.ActualWidth - popupWidth) / 2,
                    (_overlay.ActualHeight - popupHeight) / 2),
                _ => CalculateAutoPosition(targetBounds, popupWidth, popupHeight)
            };

            // Sicherstellen, dass die Popup vollständig im sichtbaren Bereich bleibt
            popupPosition.X = Math.Max(edgeMargin, Math.Min(popupPosition.X, _overlay.ActualWidth - popupWidth - edgeMargin));
            popupPosition.Y = Math.Max(edgeMargin, Math.Min(popupPosition.Y, _overlay.ActualHeight - popupHeight - edgeMargin));

            Canvas.SetLeft(_popup, popupPosition.X);
            Canvas.SetTop(_popup, popupPosition.Y);

            if (!_overlay.Children.Contains(_popup))
                _overlay.Children.Add(_popup);
        }

        /// <summary>
        /// Berechnet die Position zum Oberen Rand der Anwendung
        /// </summary>
        private System.Windows.Point CalculateTopPosition(System.Windows.Rect targetBounds, double popupWidth, double popupHeight, double margin)
        {
            var x = targetBounds.X + (targetBounds.Width - popupWidth) / 2; // Zentralisiert über dem Element
            var y = targetBounds.Y - popupHeight - margin;

            // Falls nicht genug Platz oben, nach unten verschieben
            if (y < 0)
            {
                y = targetBounds.Bottom + margin;
            }

            return new System.Windows.Point(x, y);
        }

        /// <summary>
        /// Berechnet die Position zum Unteren Rand der Anwendung
        /// </summary>
        private System.Windows.Point CalculateBottomPosition(System.Windows.Rect targetBounds, double popupWidth, double popupHeight, double margin)
        {
            var x = targetBounds.X + (targetBounds.Width - popupWidth) / 2; // Zentralisiert unter dem Element
            var y = targetBounds.Bottom + margin;

            // Falls nicht genug Platz unten, nach oben verschieben
            if (y + popupHeight > _overlay.ActualHeight)
            {
                y = targetBounds.Y - popupHeight - margin;
            }

            return new System.Windows.Point(x, y);
        }

        /// <summary>
        /// Berechnet die Position zum Linken Rand der Anwendung
        /// </summary>
        private System.Windows.Point CalculateLeftPosition(System.Windows.Rect targetBounds, double popupWidth, double popupHeight, double margin)
        {
            var x = targetBounds.X - popupWidth - margin;
            var y = targetBounds.Y + (targetBounds.Height - popupHeight) / 2; // Vertikal zentralisiert

            // Falls nicht genug Platz links, nach rechts verschieben
            if (x < 0)
            {
                x = targetBounds.Right + margin;
            }

            return new System.Windows.Point(x, y);
        }

        /// <summary>
        /// Berechnet die Position zum Rechten Rand der Anwendung
        /// </summary>
        private System.Windows.Point CalculateRightPosition(System.Windows.Rect targetBounds, double popupWidth, double popupHeight, double margin)
        {
            var x = targetBounds.Right + margin;
            var y = targetBounds.Y + (targetBounds.Height - popupHeight) / 2; // Vertikal zentralisiert

            // Falls nicht genug Platz rechts, nach links verschieben
            if (x + popupWidth > _overlay.ActualWidth)
            {
                x = targetBounds.X - popupWidth - margin;
            }

            return new System.Windows.Point(x, y);
        }

        /// <summary> 
        /// Berechnet die Position des Popups automatisch
        /// </summary>
        /// <param name="targetBounds"></param>
        /// <param name="popupWidth"></param>
        /// <param name="popupHeight"></param>
        /// <returns></returns>
        private System.Windows.Point CalculateAutoPosition(System.Windows.Rect targetBounds, double popupWidth, double popupHeight)
        {
            const double margin = 10;

            // Verfügbaren Platz in jede Richtung berechnen
            var spaceTop = targetBounds.Y;
            var spaceBottom = _overlay.ActualHeight - targetBounds.Bottom;
            var spaceLeft = targetBounds.X;
            var spaceRight = _overlay.ActualWidth - targetBounds.Right;

            // Die beste Position basierend auf verfügbarem Platz wählen
            if (spaceBottom >= popupHeight + margin && spaceBottom >= spaceTop)
            {
                // Unten positionieren
                return CalculateBottomPosition(targetBounds, popupWidth, popupHeight, margin);
            }
            else if (spaceTop >= popupHeight + margin)
            {
                // Oben positionieren
                return CalculateTopPosition(targetBounds, popupWidth, popupHeight, margin);
            }
            else if (spaceRight >= popupWidth + margin && spaceRight >= spaceLeft)
            {
                // Rechts positionieren
                return CalculateRightPosition(targetBounds, popupWidth, popupHeight, margin);
            }
            else if (spaceLeft >= popupWidth + margin)
            {
                // Links positionieren
                return CalculateLeftPosition(targetBounds, popupWidth, popupHeight, margin);
            }
            else
            {
                // Fallback: Zentrieren
                return new System.Windows.Point(
                    (_overlay.ActualWidth - popupWidth) / 2,
                    (_overlay.ActualHeight - popupHeight) / 2);
            }
        }

        /// <summary>
        /// Startet einen Timer, zum testen wie lange das Tutorial läuft
        /// </summary>
        private void StartCompletionTimer(Func<bool> condition)
        {
            _completionTimer?.Stop();
            _completionTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            _completionTimer.Tick += (s, e) =>
            {
                if (condition())
                {
                    _completionTimer.Stop();
                    ShowNextStep();
                }
            };

            _completionTimer.Start();
        }

        /// <summary>
        /// Findet im Visual-Tree ein Element das den Tutorial-Key-Tag besitzt
        /// </summary>
        public FrameworkElement? FindElementByTutorialKey(DependencyObject parent, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement fe && TutorialHelper.GetTutorialKey(fe) == key)
                {
                    return fe;
                }

                var result = FindElementByTutorialKey(child, key);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Gibt die Ränder eines Elements
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private System.Windows.Rect GetElementBounds(FrameworkElement element)
        {
            var transform = element.TransformToAncestor(_window);
            var topLeft = transform.Transform(new System.Windows.Point(0, 0));
            var bottomRight = transform.Transform(new System.Windows.Point(element.ActualWidth, element.ActualHeight));

            return new System.Windows.Rect(topLeft, bottomRight);
        }

        /// <summary>
        /// Wenn das nächste Element geladen werden sollte
        /// </summary>
        private void OnNextClicked(object? sender, EventArgs e) => ShowNextStep();

        /// <summary>
        /// Wenn das vorherige Element geladen werden sollte
        /// </summary>
        private void OnPreviousClicked(object? sender, EventArgs e) => ShowPreviousStep();

        /// <summary>
        /// Wenn das Tutorial übersprungen wird
        /// </summary>
        private void OnSkipClicked(object? sender, EventArgs e) => CompleteTutorial();

        /// <summary>
        /// Wenn das Tutorial geschlossen wird
        /// </summary>
        private void OnCloseClicked(object? sender, EventArgs e) => CancelTutorial();

        /// <summary>
        /// Markiert das Tutorial als vervollständigt
        /// </summary>
        private void CompleteTutorial()
        {
            IsActive = false;
            _overlay.Visibility = System.Windows.Visibility.Collapsed;
            _completionTimer?.Stop();
            TutorialCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Markiert das Tutorial als abgebrochen
        /// </summary>
        private void CancelTutorial()
        {
            IsActive = false;
            _overlay.Visibility = System.Windows.Visibility.Collapsed;
            _completionTimer?.Stop();
            TutorialCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Wenn der Tutorial-Manager nicht mehr benutzt wird, 
        /// freigeben (dispose) für die Garbage-Collection
        /// </summary>
        public void Dispose()
        {
            _completionTimer?.Stop();
            _popup.NextClicked -= OnNextClicked;
            _popup.PreviousClicked -= OnPreviousClicked;
            _popup.SkipClicked -= OnSkipClicked;
            _popup.CloseClicked -= OnCloseClicked;
        }
    }

    /// <summary>
    /// Stellt Event-Informationen für einen Tutorial-Schritt
    /// </summary>
    public class TutorialStepEventArgs : EventArgs
    {
        /// <summary>
        /// Gibt einen Tutorial-Schritt
        /// </summary>
        public TutorialStep Step { get; }

        /// <summary>
        /// Gibt den aktuellen Index der Tutorial-Schritten
        /// </summary>
        public int CurrentIndex { get; }

        /// <summary>
        /// Gibt die Gesammtanzahl an Tutorial-Schritten
        /// </summary>
        public int TotalSteps { get; }

        public TutorialStepEventArgs(TutorialStep step, int currentIndex, int totalSteps)
        {
            Step = step;
            CurrentIndex = currentIndex;
            TotalSteps = totalSteps;
        }
    }
}