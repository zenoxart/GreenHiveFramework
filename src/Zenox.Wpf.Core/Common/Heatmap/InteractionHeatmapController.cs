using System.Collections.Concurrent;

namespace Zenox.Wpf.Core.Common.Heatmap
{
    /// <summary>
    /// Typ der Benutzerinteraktion
    /// </summary>
    public enum InteractionType
    {
        Click,
        DoubleClick,
        RightClick,
        Hover,
        DragStart,
        DragEnd,
        KeyPress,
        Focus,
        Scroll
    }

    /// <summary>
    /// Einzelner Interaktionspunkt auf der Heatmap
    /// </summary>
    public class InteractionPoint
    {
        /// <summary>
        /// Eindeutige ID
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// X-Koordinate (relativ zum Ziel-Element oder Fenster)
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y-Koordinate (relativ zum Ziel-Element oder Fenster)
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Zeitstempel der Interaktion
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Art der Interaktion
        /// </summary>
        public InteractionType Type { get; set; }

        /// <summary>
        /// Name des Ziel-Elements (falls verfuegbar)
        /// </summary>
        public string? TargetElement { get; set; }

        /// <summary>
        /// Typ des Ziel-Elements
        /// </summary>
        public string? TargetType { get; set; }

        /// <summary>
        /// Name der View/Seite
        /// </summary>
        public string? ViewName { get; set; }

        /// <summary>
        /// Zusaetzliche Metadaten
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Dauer der Interaktion in Millisekunden (fuer Hover etc.)
        /// </summary>
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Aggregierter Heatmap-Punkt fuer die Visualisierung
    /// </summary>
    public class HeatmapCell
    {
        /// <summary>
        /// X-Position der Zelle
        /// </summary>
        public int GridX { get; set; }

        /// <summary>
        /// Y-Position der Zelle
        /// </summary>
        public int GridY { get; set; }

        /// <summary>
        /// Anzahl der Interaktionen in dieser Zelle
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Normalisierte Intensitaet (0.0 - 1.0)
        /// </summary>
        public double Intensity { get; set; }

        /// <summary>
        /// Durchschnittliche X-Position innerhalb der Zelle
        /// </summary>
        public double AverageX { get; set; }

        /// <summary>
        /// Durchschnittliche Y-Position innerhalb der Zelle
        /// </summary>
        public double AverageY { get; set; }

        /// <summary>
        /// Haeufigster Interaktionstyp in dieser Zelle
        /// </summary>
        public InteractionType DominantType { get; set; }
    }

    /// <summary>
    /// Tracking-Session fuer Interaktionen
    /// </summary>
    public class InteractionSession
    {
        /// <summary>
        /// Session-ID
        /// </summary>
        public Guid SessionId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Startzeit der Session
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Endzeit der Session
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Alle Interaktionen in dieser Session
        /// </summary>
        public List<InteractionPoint> Interactions { get; set; } = new();

        /// <summary>
        /// Session-Metadaten
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Session aktiv?
        /// </summary>
        public bool IsActive => EndTime == null;

        /// <summary>
        /// Dauer der Session
        /// </summary>
        public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
    }

    /// <summary>
    /// Konfiguration fuer den Heatmap Controller
    /// </summary>
    public class HeatmapConfiguration
    {
        /// <summary>
        /// Zellengroesse in Pixeln fuer die Aggregation
        /// </summary>
        public int CellSize { get; set; } = 20;

        /// <summary>
        /// Maximale Anzahl an gespeicherten Interaktionen
        /// </summary>
        public int MaxInteractions { get; set; } = 10000;

        /// <summary>
        /// Tracking fuer Click-Events aktiviert
        /// </summary>
        public bool TrackClicks { get; set; } = true;

        /// <summary>
        /// Tracking fuer Hover-Events aktiviert
        /// </summary>
        public bool TrackHover { get; set; } = true;

        /// <summary>
        /// Minimale Hover-Dauer in ms bevor sie getrackt wird
        /// </summary>
        public int MinHoverDurationMs { get; set; } = 500;

        /// <summary>
        /// Tracking fuer Scroll-Events aktiviert
        /// </summary>
        public bool TrackScroll { get; set; } = false;

        /// <summary>
        /// Tracking fuer Tastendruecke aktiviert
        /// </summary>
        public bool TrackKeyPress { get; set; } = false;

        /// <summary>
        /// Automatisches Speichern aktiviert
        /// </summary>
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// Intervall fuer automatisches Speichern
        /// </summary>
        public TimeSpan AutoSaveInterval { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Generischer Interaction Heatmap Controller
    /// Trackt und visualisiert Benutzerinteraktionen als Heatmap
    /// </summary>
    public sealed class InteractionHeatmapController
    {
        private static readonly Lazy<InteractionHeatmapController> _instance = 
            new(() => new InteractionHeatmapController());

        /// <summary>
        /// Singleton-Instanz
        /// </summary>
        public static InteractionHeatmapController Instance => _instance.Value;

        private readonly ConcurrentBag<InteractionPoint> _interactions;
        private readonly ConcurrentDictionary<string, InteractionSession> _sessions;
        private InteractionSession? _currentSession;
        private readonly object _sessionLock = new();
        private Timer? _autoSaveTimer;
        private bool _isTracking;

        /// <summary>
        /// Aktuelle Konfiguration
        /// </summary>
        public HeatmapConfiguration Configuration { get; private set; }

        /// <summary>
        /// Tracking aktiv?
        /// </summary>
        public bool IsTracking => _isTracking;

        /// <summary>
        /// Aktuelle Session
        /// </summary>
        public InteractionSession? CurrentSession => _currentSession;

        /// <summary>
        /// Anzahl der getrackten Interaktionen
        /// </summary>
        public int InteractionCount => _interactions.Count;

        /// <summary>
        /// Event wenn eine neue Interaktion erfasst wurde
        /// </summary>
        public event EventHandler<InteractionPoint>? InteractionRecorded;

        /// <summary>
        /// Event wenn eine Session gestartet wurde
        /// </summary>
        public event EventHandler<InteractionSession>? SessionStarted;

        /// <summary>
        /// Event wenn eine Session beendet wurde
        /// </summary>
        public event EventHandler<InteractionSession>? SessionEnded;

        /// <summary>
        /// Event wenn Heatmap-Daten aktualisiert wurden
        /// </summary>
        public event EventHandler<IReadOnlyList<HeatmapCell>>? HeatmapUpdated;

        private InteractionHeatmapController()
        {
            _interactions = new ConcurrentBag<InteractionPoint>();
            _sessions = new ConcurrentDictionary<string, InteractionSession>();
            Configuration = new HeatmapConfiguration();
        }

        /// <summary>
        /// Initialisiert den Controller mit Konfiguration
        /// </summary>
        public void Initialize(HeatmapConfiguration? configuration = null)
        {
            Configuration = configuration ?? new HeatmapConfiguration();

            if (Configuration.AutoSave)
            {
                _autoSaveTimer = new Timer(
                    _ => SaveCurrentSession(),
                    null,
                    Configuration.AutoSaveInterval,
                    Configuration.AutoSaveInterval);
            }
        }

        /// <summary>
        /// Startet das Tracking
        /// </summary>
        public void StartTracking()
        {
            if (_isTracking) return;

            _isTracking = true;
            StartNewSession();
        }

        /// <summary>
        /// Stoppt das Tracking
        /// </summary>
        public void StopTracking()
        {
            if (!_isTracking) return;

            _isTracking = false;
            EndCurrentSession();
        }

        /// <summary>
        /// Startet eine neue Tracking-Session
        /// </summary>
        public InteractionSession StartNewSession(Dictionary<string, object>? metadata = null)
        {
            lock (_sessionLock)
            {
                EndCurrentSession();

                _currentSession = new InteractionSession
                {
                    Metadata = metadata ?? new Dictionary<string, object>()
                };

                _sessions.TryAdd(_currentSession.SessionId.ToString(), _currentSession);
                SessionStarted?.Invoke(this, _currentSession);

                return _currentSession;
            }
        }

        /// <summary>
        /// Beendet die aktuelle Session
        /// </summary>
        public void EndCurrentSession()
        {
            lock (_sessionLock)
            {
                if (_currentSession == null) return;

                _currentSession.EndTime = DateTime.Now;
                SessionEnded?.Invoke(this, _currentSession);
                _currentSession = null;
            }
        }

        /// <summary>
        /// Erfasst eine Interaktion
        /// </summary>
        public void RecordInteraction(double x, double y, InteractionType type,
            string? targetElement = null, string? targetType = null, string? viewName = null,
            Dictionary<string, object>? metadata = null, long durationMs = 0)
        {
            if (!_isTracking) return;

            // Filter basierend auf Konfiguration
            if (!ShouldTrack(type)) return;

            var interaction = new InteractionPoint
            {
                X = x,
                Y = y,
                Type = type,
                TargetElement = targetElement,
                TargetType = targetType,
                ViewName = viewName,
                Metadata = metadata ?? new Dictionary<string, object>(),
                DurationMs = durationMs
            };

            _interactions.Add(interaction);
            _currentSession?.Interactions.Add(interaction);

            // Limit einhalten
            EnforceMaxInteractions();

            InteractionRecorded?.Invoke(this, interaction);

            // Telemetrie loggen
            Telemetry.TelemetryService.Instance?.LogEvent(
                $"Interaction_{type}",
                "Heatmap",
                new Dictionary<string, object>
                {
                    ["X"] = x,
                    ["Y"] = y,
                    ["Target"] = targetElement ?? "Unknown",
                    ["View"] = viewName ?? "Unknown"
                });
        }

        /// <summary>
        /// Erfasst einen Click
        /// </summary>
        public void RecordClick(double x, double y, string? targetElement = null,
            string? targetType = null, string? viewName = null)
        {
            RecordInteraction(x, y, InteractionType.Click, targetElement, targetType, viewName);
        }

        /// <summary>
        /// Erfasst einen Hover
        /// </summary>
        public void RecordHover(double x, double y, long durationMs,
            string? targetElement = null, string? targetType = null, string? viewName = null)
        {
            if (durationMs < Configuration.MinHoverDurationMs) return;
            RecordInteraction(x, y, InteractionType.Hover, targetElement, targetType, viewName, durationMs: durationMs);
        }

        /// <summary>
        /// Generiert Heatmap-Zellen aus den Interaktionen
        /// </summary>
        public IReadOnlyList<HeatmapCell> GenerateHeatmap(double width, double height,
            InteractionType? filterType = null, DateTime? fromTime = null, DateTime? toTime = null)
        {
            var filteredInteractions = _interactions.AsEnumerable();

            // Filter anwenden
            if (filterType.HasValue)
                filteredInteractions = filteredInteractions.Where(i => i.Type == filterType.Value);

            if (fromTime.HasValue)
                filteredInteractions = filteredInteractions.Where(i => i.Timestamp >= fromTime.Value);

            if (toTime.HasValue)
                filteredInteractions = filteredInteractions.Where(i => i.Timestamp <= toTime.Value);

            var interactionList = filteredInteractions.ToList();

            if (!interactionList.Any())
                return Array.Empty<HeatmapCell>();

            var cellSize = Configuration.CellSize;
            var gridWidth = (int)Math.Ceiling(width / cellSize);
            var gridHeight = (int)Math.Ceiling(height / cellSize);

            // Interaktionen in Zellen gruppieren
            var cellData = new Dictionary<(int, int), List<InteractionPoint>>();

            foreach (var interaction in interactionList)
            {
                var gridX = Math.Min((int)(interaction.X / cellSize), gridWidth - 1);
                var gridY = Math.Min((int)(interaction.Y / cellSize), gridHeight - 1);

                if (gridX < 0 || gridY < 0) continue;

                var key = (gridX, gridY);
                if (!cellData.ContainsKey(key))
                    cellData[key] = new List<InteractionPoint>();

                cellData[key].Add(interaction);
            }

            // Zellen erstellen und normalisieren
            var maxCount = cellData.Values.Max(v => v.Count);
            var cells = new List<HeatmapCell>();

            foreach (var kvp in cellData)
            {
                var points = kvp.Value;
                var dominantType = points
                    .GroupBy(p => p.Type)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

                cells.Add(new HeatmapCell
                {
                    GridX = kvp.Key.Item1,
                    GridY = kvp.Key.Item2,
                    Count = points.Count,
                    Intensity = (double)points.Count / maxCount,
                    AverageX = points.Average(p => p.X),
                    AverageY = points.Average(p => p.Y),
                    DominantType = dominantType
                });
            }

            HeatmapUpdated?.Invoke(this, cells);
            return cells;
        }

        /// <summary>
        /// Gibt Statistiken zurueck
        /// </summary>
        public HeatmapStatistics GetStatistics(string? viewName = null)
        {
            var interactions = viewName != null
                ? _interactions.Where(i => i.ViewName == viewName).ToList()
                : _interactions.ToList();

            if (!interactions.Any())
                return new HeatmapStatistics();

            var typeGroups = interactions.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.Count());

            return new HeatmapStatistics
            {
                TotalInteractions = interactions.Count,
                ClickCount = typeGroups.GetValueOrDefault(InteractionType.Click),
                HoverCount = typeGroups.GetValueOrDefault(InteractionType.Hover),
                ScrollCount = typeGroups.GetValueOrDefault(InteractionType.Scroll),
                UniqueElements = interactions.Where(i => i.TargetElement != null)
                    .Select(i => i.TargetElement).Distinct().Count(),
                UniqueViews = interactions.Where(i => i.ViewName != null)
                    .Select(i => i.ViewName).Distinct().Count(),
                AverageHoverDurationMs = interactions
                    .Where(i => i.Type == InteractionType.Hover && i.DurationMs > 0)
                    .Select(i => i.DurationMs)
                    .DefaultIfEmpty(0)
                    .Average(),
                MostClickedElement = interactions
                    .Where(i => i.Type == InteractionType.Click && i.TargetElement != null)
                    .GroupBy(i => i.TargetElement)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key,
                SessionCount = _sessions.Count
            };
        }

        /// <summary>
        /// Gibt die Top-Elemente nach Interaktionen zurueck
        /// </summary>
        public IReadOnlyList<ElementInteractionInfo> GetTopElements(int count = 10, InteractionType? type = null)
        {
            var interactions = type.HasValue
                ? _interactions.Where(i => i.Type == type.Value)
                : _interactions.AsEnumerable();

            return interactions
                .Where(i => !string.IsNullOrEmpty(i.TargetElement))
                .GroupBy(i => new { i.TargetElement, i.TargetType })
                .Select(g => new ElementInteractionInfo
                {
                    ElementName = g.Key.TargetElement!,
                    ElementType = g.Key.TargetType,
                    InteractionCount = g.Count(),
                    LastInteraction = g.Max(i => i.Timestamp),
                    InteractionTypes = g.Select(i => i.Type).Distinct().ToList()
                })
                .OrderByDescending(e => e.InteractionCount)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Loescht alle Interaktionen
        /// </summary>
        public void ClearInteractions()
        {
            while (_interactions.TryTake(out _)) { }
            _currentSession?.Interactions.Clear();
        }

        /// <summary>
        /// Speichert die aktuelle Session
        /// </summary>
        public void SaveCurrentSession()
        {
            // Hier koennte Persistierung implementiert werden
            // z.B. JSON-Export oder Datenbank-Speicherung
        }

        /// <summary>
        /// Exportiert Interaktionen als JSON
        /// </summary>
        public string ExportToJson()
        {
            var data = new
            {
                ExportTime = DateTime.Now,
                Configuration,
                Statistics = GetStatistics(),
                Interactions = _interactions.ToList(),
                Sessions = _sessions.Values.ToList()
            };

            return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private bool ShouldTrack(InteractionType type)
        {
            return type switch
            {
                InteractionType.Click or InteractionType.DoubleClick or InteractionType.RightClick
                    => Configuration.TrackClicks,
                InteractionType.Hover => Configuration.TrackHover,
                InteractionType.Scroll => Configuration.TrackScroll,
                InteractionType.KeyPress => Configuration.TrackKeyPress,
                _ => true
            };
        }

        private void EnforceMaxInteractions()
        {
            while (_interactions.Count > Configuration.MaxInteractions)
            {
                _interactions.TryTake(out _);
            }
        }
    }

    /// <summary>
    /// Heatmap-Statistiken
    /// </summary>
    public class HeatmapStatistics
    {
        public int TotalInteractions { get; set; }
        public int ClickCount { get; set; }
        public int HoverCount { get; set; }
        public int ScrollCount { get; set; }
        public int UniqueElements { get; set; }
        public int UniqueViews { get; set; }
        public double AverageHoverDurationMs { get; set; }
        public string? MostClickedElement { get; set; }
        public int SessionCount { get; set; }
    }

    /// <summary>
    /// Informationen ueber Element-Interaktionen
    /// </summary>
    public class ElementInteractionInfo
    {
        public string ElementName { get; set; } = string.Empty;
        public string? ElementType { get; set; }
        public int InteractionCount { get; set; }
        public DateTime LastInteraction { get; set; }
        public List<InteractionType> InteractionTypes { get; set; } = new();
    }
}
