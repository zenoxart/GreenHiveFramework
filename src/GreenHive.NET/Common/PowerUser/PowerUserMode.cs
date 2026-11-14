namespace GreenHive.NET.Core.Common.PowerUser
{
	/// <summary>
	/// Represents different power-user features
	/// </summary>
	[Flags]
	public enum PowerUserFeature
	{
		None = 0,
		PerformanceOverlay = 1 << 0,
		ExecutionLog = 1 << 1,
		BackgroundTasks = 1 << 2,
		Telemetry = 1 << 3,
		AdvancedCommands = 1 << 4,
		DebugInfo = 1 << 5,
		Snapshots = 1 << 6,
		CommandReplay = 1 << 7,
		All = PerformanceOverlay | ExecutionLog | BackgroundTasks | Telemetry |
			  AdvancedCommands | DebugInfo | Snapshots | CommandReplay
	}

	/// <summary>
	/// MVP Power-User Mode
	/// Manages advanced features visibility and availability
	/// </summary>
	public class PowerUserMode
	{
		private static readonly Lazy<PowerUserMode> _instance = new(() => new PowerUserMode());
		public static PowerUserMode Instance => _instance.Value;

		private bool _isEnabled;
		private PowerUserFeature _enabledFeatures;
		private readonly object _lock = new();

		public event EventHandler<PowerUserModeChangedEventArgs> ModeChanged;
		public event EventHandler<PowerUserFeature> FeatureToggled;

		private PowerUserMode()
		{
			_isEnabled = false;
			_enabledFeatures = PowerUserFeature.None;
			LoadSettings();
		}

		/// <summary>
		/// Is power-user mode enabled
		/// </summary>
		public bool IsEnabled
		{
			get
			{
				lock (_lock) { return _isEnabled; }
			}
			set
			{
				lock (_lock)
				{
					if (_isEnabled != value)
					{
						var oldValue = _isEnabled;
						_isEnabled = value;

						if (_isEnabled)
						{
							// Enable default features
							_enabledFeatures = PowerUserFeature.PerformanceOverlay |
											 PowerUserFeature.ExecutionLog |
											 PowerUserFeature.AdvancedCommands;
						}
						else
						{
							_enabledFeatures = PowerUserFeature.None;
						}

						SaveSettings();
						ModeChanged?.Invoke(this, new PowerUserModeChangedEventArgs(oldValue, value, _enabledFeatures));
					}
				}
			}
		}

		/// <summary>
		/// Toggle power-user mode
		/// </summary>
		public void Toggle()
		{
			IsEnabled = !IsEnabled;
		}

		/// <summary>
		/// Check if a specific feature is enabled
		/// </summary>
		public bool IsFeatureEnabled(PowerUserFeature feature)
		{
			lock (_lock)
			{
				return _isEnabled && _enabledFeatures.HasFlag(feature);
			}
		}

		/// <summary>
		/// Enable a specific feature
		/// </summary>
		public void EnableFeature(PowerUserFeature feature)
		{
			lock (_lock)
			{
				if (!_enabledFeatures.HasFlag(feature))
				{
					_enabledFeatures |= feature;
					SaveSettings();
					FeatureToggled?.Invoke(this, feature);
				}
			}
		}

		/// <summary>
		/// Disable a specific feature
		/// </summary>
		public void DisableFeature(PowerUserFeature feature)
		{
			lock (_lock)
			{
				if (_enabledFeatures.HasFlag(feature))
				{
					_enabledFeatures &= ~feature;
					SaveSettings();
					FeatureToggled?.Invoke(this, feature);
				}
			}
		}

		/// <summary>
		/// Toggle a specific feature
		/// </summary>
		public void ToggleFeature(PowerUserFeature feature)
		{
			if (IsFeatureEnabled(feature))
				DisableFeature(feature);
			else
				EnableFeature(feature);
		}

		/// <summary>
		/// Get all enabled features
		/// </summary>
		public PowerUserFeature GetEnabledFeatures()
		{
			lock (_lock)
			{
				return _enabledFeatures;
			}
		}

		/// <summary>
		/// Set enabled features
		/// </summary>
		public void SetEnabledFeatures(PowerUserFeature features)
		{
			lock (_lock)
			{
				_enabledFeatures = features;
				SaveSettings();
			}
		}

		/// <summary>
		/// Get feature display name
		/// </summary>
		public static string GetFeatureName(PowerUserFeature feature)
		{
			return feature switch
			{
				PowerUserFeature.PerformanceOverlay => "Performance-Overlay",
				PowerUserFeature.ExecutionLog => "Execution Log",
				PowerUserFeature.BackgroundTasks => "Background Tasks",
				PowerUserFeature.Telemetry => "Telemetrie-Ansicht",
				PowerUserFeature.AdvancedCommands => "Erweiterte Befehle",
				PowerUserFeature.DebugInfo => "Debug-Informationen",
				PowerUserFeature.Snapshots => "Snapshot-Verwaltung",
				PowerUserFeature.CommandReplay => "Befehlswiedergabe",
				_ => feature.ToString()
			};
		}

		private void LoadSettings()
		{
			try
			{
				var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				var settingsPath = System.IO.Path.Combine(appData, "VoestalpineWpf", "poweruser_settings.json");

				if (System.IO.File.Exists(settingsPath))
				{
					var json = System.IO.File.ReadAllText(settingsPath);
					var settings = System.Text.Json.JsonSerializer.Deserialize<PowerUserSettings>(json);
					if (settings != null)
					{
						_isEnabled = settings.IsEnabled;
						_enabledFeatures = settings.EnabledFeatures;
					}
				}
			}
			catch { }
		}

		private void SaveSettings()
		{
			try
			{
				var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				var settingsDir = System.IO.Path.Combine(appData, "VoestalpineWpf");
				System.IO.Directory.CreateDirectory(settingsDir);

				var settingsPath = System.IO.Path.Combine(settingsDir, "poweruser_settings.json");
				var settings = new PowerUserSettings
				{
					IsEnabled = _isEnabled,
					EnabledFeatures = _enabledFeatures
				};

				var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
				{
					WriteIndented = true
				});
				System.IO.File.WriteAllText(settingsPath, json);
			}
			catch { }
		}

		private class PowerUserSettings
		{
			public bool IsEnabled { get; set; }
			public PowerUserFeature EnabledFeatures { get; set; }
		}
	}

	/// <summary>
	/// Event args for power-user mode changes
	/// </summary>
	public class PowerUserModeChangedEventArgs : EventArgs
	{
		public bool OldValue { get; }
		public bool NewValue { get; }
		public PowerUserFeature EnabledFeatures { get; }

		public PowerUserModeChangedEventArgs(bool oldValue, bool newValue, PowerUserFeature enabledFeatures)
		{
			OldValue = oldValue;
			NewValue = newValue;
			EnabledFeatures = enabledFeatures;
		}
	}
}
