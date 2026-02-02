using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;
using Zenox.Wpf.Core.Common.Strategies;

namespace Zenox.Wpf.Views;

/// <summary>
/// ViewModel für die StrategyDemoView.
/// Demonstriert die Verwendung des StrategyProvider-Frameworks.
/// </summary>
public class StrategyDemoViewModel : NotificationObject
{
    private readonly StrategyProvider<string, string> _strategyProvider;

    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _usedStrategyInfo = string.Empty;
    private IStrategy<string, string>? _selectedStrategy;

    public StrategyDemoViewModel()
    {
        _strategyProvider = new StrategyProvider<string, string>("TextTransformProvider");
        
        RegisterStrategies();
        InitializeCommands();
        
        AvailableStrategies = new ObservableCollection<IStrategy<string, string>>(
            _strategyProvider.GetAllStrategies().OrderByDescending(s => s.Priority));
    }

    /// <summary>
    /// Registriert alle verfügbaren Strategien beim Provider
    /// </summary>
    private void RegisterStrategies()
    {
        // Grundlegende Textstrategien
        _strategyProvider.Register(new UpperCaseStrategy());
        _strategyProvider.Register(new LowerCaseStrategy());
        _strategyProvider.Register(new ReverseStrategy());
        
        // Datenformatierungsstrategien
        _strategyProvider.Register(new NumberFormatterStrategy());
        _strategyProvider.Register(new EmailFormatterStrategy());
        _strategyProvider.Register(new JsonParserStrategy());
        _strategyProvider.Register(new UrlParserStrategy());
        _strategyProvider.Register(new DateTimeParserStrategy());
        
        // Sicherheits- & Codierungsstrategien
        _strategyProvider.Register(new Base64EncodeStrategy());
        _strategyProvider.Register(new Base64DecodeStrategy());
        _strategyProvider.Register(new HashCalculatorStrategy());
        
        // Textanalysestrategien
        _strategyProvider.Register(new TextStatisticsStrategy());
        _strategyProvider.Register(new WordFrequencyStrategy());
        
        // Validierungsstrategien
        _strategyProvider.Register(new IpAddressStrategy());
        _strategyProvider.Register(new CreditCardValidatorStrategy());
        
        // Entwicklungsstrategien
        _strategyProvider.Register(new SqlFormatterStrategy());
        _strategyProvider.Register(new CaseConverterStrategy());
    }

    /// <summary>
    /// Initialisiert die Commands
    /// </summary>
    private void InitializeCommands()
    {
        ExecuteAutoCommand = new RelayCommand(
            _ => ExecuteAuto(),
            _ => !string.IsNullOrEmpty(InputText))
        { Name = "ExecuteAutoStrategy" };

        ExecuteSelectedCommand = new RelayCommand(
            _ => ExecuteSelected(),
            _ => !string.IsNullOrEmpty(InputText) && SelectedStrategy != null)
        { Name = "ExecuteSelectedStrategy" };
    }

    /// <summary>
    /// Der Eingabetext
    /// </summary>
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    /// <summary>
    /// Das Ergebnis der Strategieausführung
    /// </summary>
    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, value);
    }

    /// <summary>
    /// Information über die verwendete Strategie
    /// </summary>
    public string UsedStrategyInfo
    {
        get => _usedStrategyInfo;
        set => SetProperty(ref _usedStrategyInfo, value);
    }

    /// <summary>
    /// Die ausgewählte Strategie für manuelle Ausführung
    /// </summary>
    public IStrategy<string, string>? SelectedStrategy
    {
        get => _selectedStrategy;
        set => SetProperty(ref _selectedStrategy, value);
    }

    /// <summary>
    /// Liste aller verfügbaren Strategien
    /// </summary>
    public ObservableCollection<IStrategy<string, string>> AvailableStrategies { get; }

    /// <summary>
    /// Command für automatische Strategieauswahl
    /// </summary>
    public ICommand ExecuteAutoCommand { get; private set; } = null!;

    /// <summary>
    /// Command für Ausführung der ausgewählten Strategie
    /// </summary>
    public ICommand ExecuteSelectedCommand { get; private set; } = null!;

    /// <summary>
    /// Führt die automatische Strategieauswahl aus
    /// </summary>
    private void ExecuteAuto()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            UsedStrategyInfo = string.Empty;
            return;
        }

        // Finde die beste Strategie basierend auf Input und Priorität
        var strategies = _strategyProvider.GetAllStrategies()
            .Where(s => s.CanHandle(InputText))
            .OrderByDescending(s => s.Priority)
            .ToList();

        if (strategies.Count == 0)
        {
            OutputText = "Keine passende Strategie gefunden.";
            UsedStrategyInfo = string.Empty;
            return;
        }

        var bestStrategy = strategies.First();
        
        if (_strategyProvider.TryExecute(InputText, out var result))
        {
            OutputText = result;
            UsedStrategyInfo = $"Automatisch: {bestStrategy.Name} (Priorität: {bestStrategy.Priority}) - {bestStrategy.Description}";
        }
    }

    /// <summary>
    /// Führt die manuell ausgewählte Strategie aus
    /// </summary>
    private void ExecuteSelected()
    {
        if (string.IsNullOrEmpty(InputText) || SelectedStrategy == null)
        {
            OutputText = string.Empty;
            UsedStrategyInfo = string.Empty;
            return;
        }

        try
        {
            OutputText = _strategyProvider.ExecuteStrategy(SelectedStrategy.Name, InputText);
            UsedStrategyInfo = $"Manuell: {SelectedStrategy.Name} (Priorität: {SelectedStrategy.Priority}) - {SelectedStrategy.Description}";
        }
        catch (InvalidOperationException ex)
        {
            OutputText = $"Fehler: {ex.Message}";
            UsedStrategyInfo = "Ausführung fehlgeschlagen";
        }
    }
}
