using CsvEditSharp.Bindings;
using CsvEditSharp.Models;
using CsvEditSharp.Services;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CsvEditSharp.ViewModels
{
    public class MainWindowViewModel : BindableBase, IDisposable
    {
        private static readonly string CsvFileFilter = "CSV File|*.csv|Plain Text File|*.txt|All Files|*.*";

        private ObservableCollection<object> _csvRows;
        private ObservableCollection<string> _errorMessages = new ObservableCollection<string>();
        private bool _hasErrorMessages = false;
        private IDocument _configurationDoc;
        private IDocument _queryDoc;
        private CsvEditSharpConfigurationHost _host;
        private IViewServiceProvider _viewService;
        private int _selectedTab;

        private string _currentFilePath;
        private string _currentFileName;
        private string _currentConfigName;
        private string _selectedTemplate;

        private ICommand _readCsvCmd;
        private ICommand _queryCmd;
        private ICommand _writeCsvCmd;
        private ICommand _writeNewCsvCmd;
        private ICommand _runConfigComannd;
        private ICommand _resetQueryCmd;
        private ICommand _saveConfigCmd;
        private ICommand _saveConfigAsCmd;
        private ICommand _configSettingsCmd;
        private ICommand _deleteTemplateCmd;

        private CsvEditSharpWorkspace Workspace { get; }

        public int SelectedTab
        {
            get { return _selectedTab; }
            set { SetProperty(ref _selectedTab, value); }
        }

        public string SelectedTemplate
        {
            get { return _selectedTemplate; }
            set { SetProperty(ref _selectedTemplate, value); }
        }

        public string CurrentFilePath
        {
            get { return _currentFilePath; }
            set { SetProperty(ref _currentFilePath, value); }
        }

        public string CurrentFileName
        {
            get { return _currentFileName; }
            set { SetProperty(ref _currentFileName, value); }
        }

        public string CurrentConfigName
        {
            get { return _currentConfigName; }
            set
            {
                if (value == "") { value = "(Empty)"; }
                SetProperty(ref _currentConfigName, value);
            }
        }

        public ObservableCollection<object> CsvRows
        {
            get { return _csvRows; }
            set { SetProperty(ref _csvRows, value); }
        }

        public ObservableCollection<string> ErrorMessages
        {
            get { return _errorMessages; }
            set { SetProperty(ref _errorMessages, value); }
        }

        public bool HasErrorMessages
        {
            get { return _hasErrorMessages; }
            set { SetProperty(ref _hasErrorMessages, value); }
        }

        public ObservableCollection<string> ConfigFileTemplates
        {
            get { return CsvConfigFileManager.Default.SettingsList; }
        }

        public IDocument ConfigurationDoc
        {
            get { return _configurationDoc; }
            set { SetProperty(ref _configurationDoc, value); }
        }

        public IDocument QueryDoc
        {
            get { return _queryDoc; }
            set { SetProperty(ref _queryDoc, value); }
        }

        public ICommand ReadCsvCommand
        {
            get
            {
                if (_readCsvCmd == null)
                {
                    _readCsvCmd = new DelegateCommand(_ => ReadCsvAsync());
                }
                return _readCsvCmd;
            }
        }

        public ICommand WriteCsvCommand
        {
            get
            {
                if (_writeCsvCmd == null)
                {
                    _writeCsvCmd = new DelegateCommand(_ => WriteCsv(), _ => (CsvRows?.Count ?? 0) > 0);
                }
                return _writeCsvCmd;
            }
        }

        public ICommand WriteNewCsvCommand
        {
            get
            {
                if (_writeNewCsvCmd == null)
                {
                    _writeNewCsvCmd = new DelegateCommand(_ => WriteNewCsv(), _ => (CsvRows?.Count ?? 0) > 0);
                }
                return _writeNewCsvCmd;
            }
        }

        public ICommand QueryCommand
        {
            get
            {
                if (_queryCmd == null)
                {
                    _queryCmd = new DelegateCommand(async _ => await ExecuteQueryAsync(), _ => Workspace.HasScriptState);
                }
                return _queryCmd;
            }
        }

        public ICommand ResetQueryCommand
        {
            get
            {
                if (_resetQueryCmd == null)
                {
                    _resetQueryCmd = new DelegateCommand(_ => ResetQuery(), _ => Workspace.HasScriptState);
                }
                return _resetQueryCmd;
            }
        }

        public ICommand RunConfigCommand
        {
            get
            {
                if (_runConfigComannd == null)
                {
                    _runConfigComannd = new DelegateCommand(async _ => await RunConfigurationAsync(), _ => CanExecuteRunConfigCommand());
                }
                return _runConfigComannd;
            }
        }

        public ICommand SaveConfigCommand
        {
            get
            {
                if (_saveConfigCmd == null)
                {
                    _saveConfigCmd = new DelegateCommand(_ => SaveConfigFile(), _ => !string.IsNullOrWhiteSpace(ConfigurationDoc.Text)
                        && File.Exists(CsvConfigFileManager.Default.CurrentConfigFilePath));
                }
                return _saveConfigCmd;
            }
        }

        public ICommand SaveConfigAsCommand
        {
            get
            {
                if (_saveConfigAsCmd == null)
                {
                    _saveConfigAsCmd = new DelegateCommand(_ => SaveConfigAs(), _ => !string.IsNullOrWhiteSpace(ConfigurationDoc.Text)
                        && File.Exists(CsvConfigFileManager.Default.CurrentConfigFilePath));
                }
                return _saveConfigAsCmd;
            }
        }

        public ICommand ConfigSettingsCommand
        {
            get
            {
                if (_configSettingsCmd == null)
                {
                    _configSettingsCmd = new DelegateCommand(_ => _viewService.ConfigSettingsDialogService.ShowModal(), _ => true);
                }
                return _configSettingsCmd;
            }
        }

        public ICommand DeleteTemplateCommand
        {
            get
            {
                if (_deleteTemplateCmd == null)
                {
                    _deleteTemplateCmd = new DelegateCommand(_ => CsvConfigFileManager.Default.RemoveConfigFile(SelectedTemplate),
                        _ => !string.IsNullOrEmpty(SelectedTemplate));
                }
                return _deleteTemplateCmd;
            }
        }

        public MainWindowViewModel(IViewServiceProvider viewServiceProvider)
        {
            _viewService = viewServiceProvider;
            _errorMessages.CollectionChanged += (_, __) => HasErrorMessages = _errorMessages.Count > 0;
            _host = new CsvEditSharpConfigurationHost();
            Workspace = new CsvEditSharpWorkspace(_host, _errorMessages);

            ConfigurationDoc = new TextDocument(StringTextSource.Empty);
            QueryDoc = new TextDocument(new StringTextSource("Query<FieldData>( records => records.Where(row => true).OrderBy(row => row) );"));

            CurrentFilePath = string.Empty;
            CurrentFileName = "(Empty)";
            CurrentConfigName = "(Empty)";
            SelectedTemplate = ConfigFileTemplates.First();
            SelectedTab = 0;
        }

        public DataGridColumnValidationRule GetDataGridColumnValidation(string propertyName)
        {
            ColumnValidation columnValidaiton;
            if (_host.ColumnValidations.TryGetValue(propertyName, out columnValidaiton))
            {
                return new DataGridColumnValidationRule(columnValidaiton.Validation, columnValidaiton.ErrorMessage);
            }
            return null;
        }

        private bool CanExecuteRunConfigCommand()
        {
            return (CurrentFilePath != null)
                && File.Exists(CurrentFilePath)
                && !string.IsNullOrWhiteSpace(ConfigurationDoc.Text);
        }

        private async void ReadCsvAsync()
        {
            //OpenFileDialog
            var openFileService = _viewService.OpenFileSelectionService;
            CurrentFilePath = openFileService.SelectFile("Select a CSV File", CsvFileFilter, null);
            if (!File.Exists(CurrentFilePath)) { return; }

            var configText = CsvConfigFileManager.Default.GetCsvConfigString(CurrentFilePath, _selectedTemplate);

            CurrentConfigName = Path.GetFileName(CsvConfigFileManager.Default.CurrentConfigFilePath);
            CurrentFileName = Path.GetFileName(CurrentFilePath);

            ConfigurationDoc.Text = configText;

            await RunConfigurationAsync();
        }

        private async Task RunConfigurationAsync()
        {
            _host.Reset();
            ErrorMessages.Clear();
            await Workspace.RunScriptAsync(ConfigurationDoc.Text);

            try
            {
                using (var stream = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Read))
                using (var reader = new StreamReader(stream, _host.Encoding ?? Encoding.Default))
                {
                    _host.Read(reader);
                }
            }
            catch (Exception e)
            {
                ErrorMessages.Add(e.ToString());
            }

            CsvRows = new ObservableCollection<object>(_host.Records);
            SelectedTab = 0;
        }

        private void WriteCsv()
        {
            try
            {
                using (var writer = new StreamWriter(_currentFilePath, false, _host.Encoding))
                {
                    _host.Write(writer, CsvRows);
                }
            }
            catch (Exception e)
            {
                ErrorMessages.Add(e.ToString());
            }
        }

        private void WriteNewCsv()
        {
            var saveFileService = _viewService.SaveFileSelectionService;
            var fileName = saveFileService.SelectFile("Save As..", CsvFileFilter, _currentFilePath);
            if (fileName == null) { return; }

            try
            {
                using (var writer = new StreamWriter(fileName, false, _host.Encoding))
                {
                    _host.Write(writer, CsvRows);
                }
            }
            catch (Exception e)
            {
                ErrorMessages.Add(e.ToString());
            }
        }

        private async Task ExecuteQueryAsync()
        {
            ErrorMessages.Clear();
            await Workspace.ContinueScriptAsync(QueryDoc.Text);
            try
            {
                _host.ExecuteQuery();
                CsvRows = new ObservableCollection<object>(_host.Records);
            }
            catch (Exception e)
            {
                ErrorMessages.Add(e.ToString());
            }
        }

        private void ResetQuery()
        {
            _host.ResetQuery();
			_errorMessages.Clear();
            CsvRows = new ObservableCollection<object>(_host.Records);
        }

        private void SaveConfigFile()
        {
            CsvConfigFileManager.Default.SaveConfigFile(ConfigurationDoc.Text);
        }

        private void SaveConfigAs()
        {
            var service = _viewService.SaveConfigDialogService;
            if (true == service.ShowModal())
            {
                var fileName = string.Empty;
                if (service.Result.IsTemplate)
                {
                    fileName = CsvConfigFileManager.Default.MakeCurrentConfigFilePath(service.Result.TemplateName);
                }
                else
                {
                    fileName = Path.Combine(Path.GetDirectoryName(CurrentFilePath), "Default.config.csx");
                }
                CurrentConfigName = Path.GetFileName(fileName);
                CsvConfigFileManager.Default.CurrentConfigFilePath = fileName;
                CsvConfigFileManager.Default.SaveConfigFile(ConfigurationDoc.Text);

            }
        }

        public async Task<IEnumerable<CompletionData>> GetCompletionListAsync(int position, string code)
        {
            return await Workspace.GetCompletionListAsync(position, code);
        }

        public DataGridColumnConverter GetDataGridColumnConverter(string propertyName)
        {
            var propertyMap = _host.ClassMapForWriting?.PropertyMaps?.FirstOrDefault(m => m.Data.Property.Name == propertyName);
            if (propertyMap == null) { return null; }

            return new DataGridColumnConverter(
                propertyMap.Data.Names[propertyMap.Data.NameIndex],
                propertyMap.Data.TypeConverter,
                propertyMap.Data.TypeConverterOptions);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Workspace?.Dispose();
            }
        }

        ~MainWindowViewModel()
        {
            Dispose(false);
        }

    }
}
