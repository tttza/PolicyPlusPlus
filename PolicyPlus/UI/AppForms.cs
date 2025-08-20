using System;
using System.Windows.Forms;

namespace PolicyPlus.UI;

// Centralized factory replacing VB My.MyProject.Forms singletons
internal static class AppForms
{
    // Lazy single-instance dialogs (recreated if disposed) matching old VB semantics
    public static T Get<T>(ref T instance) where T : Form, new()
    {
        if (instance == null || instance.IsDisposed)
            instance = new T();
        return instance;
    }

    public static PolicyDetail.EditPolKey EditPolKey => Get(ref _editPolKey);
    private static PolicyDetail.EditPolKey _editPolKey;
    public static PolicyDetail.EditPolStringData EditPolStringData => Get(ref _editPolStringData);
    private static PolicyDetail.EditPolStringData _editPolStringData;
    public static PolicyDetail.EditPolNumericData EditPolNumericData => Get(ref _editPolNumericData);
    private static PolicyDetail.EditPolNumericData _editPolNumericData;
    public static PolicyDetail.EditPolMultiStringData EditPolMultiStringData => Get(ref _editPolMultiStringData);
    private static PolicyDetail.EditPolMultiStringData _editPolMultiStringData;
    public static PolicyDetail.EditPolValue EditPolValue => Get(ref _editPolValue);
    private static PolicyDetail.EditPolValue _editPolValue;
    public static PolicyDetail.EditPolDelete EditPolDelete => Get(ref _editPolDelete);
    private static PolicyDetail.EditPolDelete _editPolDelete;
    public static ImportReg ImportReg => Get(ref _importReg);
    private static ImportReg _importReg;
    public static Export.ExportReg ExportReg => Get(ref _exportReg);
    private static Export.ExportReg _exportReg;
    public static ListEditor ListEditor => Get(ref _listEditor);
    private static ListEditor _listEditor;
    public static PolicyDetail.DetailPolicyFormatted DetailPolicyFormatted => Get(ref _detailPolicyFormatted);
    private static PolicyDetail.DetailPolicyFormatted _detailPolicyFormatted;
    public static PolicyDetail.EditSetting EditSetting => Get(ref _editSetting);
    private static PolicyDetail.EditSetting _editSetting;
    public static Find.FindResults FindResults => Get(ref _findResults);
    private static Find.FindResults _findResults;
    public static Find.FindById FindById => Get(ref _findById);
    private static Find.FindById _findById;
    public static OpenAdmxFolder OpenAdmxFolder => Get(ref _openAdmxFolder);
    private static OpenAdmxFolder _openAdmxFolder;
    public static OpenPol OpenPol => Get(ref _openPol);
    private static OpenPol _openPol;
    public static CategoryDetail.DetailProduct DetailProduct => Get(ref _detailProduct);
    private static CategoryDetail.DetailProduct _detailProduct;
    public static CategoryDetail.DetailSupport DetailSupport => Get(ref _detailSupport);
    private static CategoryDetail.DetailSupport _detailSupport;
    public static CategoryDetail.DetailCategory DetailCategory => Get(ref _detailCategory);
    private static CategoryDetail.DetailCategory _detailCategory;
    public static PolicyDetail.DetailPolicy DetailPolicy => Get(ref _detailPolicy);
    private static PolicyDetail.DetailPolicy _detailPolicy;
    public static Admx.DetailAdmx DetailAdmx => Get(ref _detailAdmx);
    private static Admx.DetailAdmx _detailAdmx;
    public static OpenUserRegistry OpenUserRegistry => Get(ref _openUserRegistry);
    private static OpenUserRegistry _openUserRegistry;
    public static OpenUserGpo OpenUserGpo => Get(ref _openUserGpo);
    private static OpenUserGpo _openUserGpo;
    public static Main.Main Main => Get(ref _mainForm);
    private static Main.Main _mainForm;
    public static Find.FindByText FindByText => Get(ref _findByText);
    private static Find.FindByText _findByText;
    public static Find.FindByRegistry FindByRegistry => Get(ref _findByRegistry);
    private static Find.FindByRegistry _findByRegistry;
    public static Find.FilterOptions FilterOptions => Get(ref _filterOptions);
    private static Find.FilterOptions _filterOptions;
    public static ImportSpol ImportSpol => Get(ref _importSpol);
    private static ImportSpol _importSpol;
    public static OpenSection OpenSection => Get(ref _openSection);
    private static OpenSection _openSection;
    public static Admx.DownloadAdmx DownloadAdmx => Get(ref _downloadAdmx);
    private static Admx.DownloadAdmx _downloadAdmx;
    public static LoadedAdmx LoadedAdmx => Get(ref _loadedAdmx);
    private static LoadedAdmx _loadedAdmx;
    public static LoadedSupportDefinitions LoadedSupportDefinitions => Get(ref _loadedSupportDefinitions);
    private static LoadedSupportDefinitions _loadedSupportDefinitions;
    public static LoadedProducts LoadedProducts => Get(ref _loadedProducts);
    private static LoadedProducts _loadedProducts;
    public static LanguageOptions LanguageOptions => Get(ref _languageOptions);
    private static LanguageOptions _languageOptions;
    public static InspectPolicyElements InspectPolicyElements => Get(ref _inspectPolicyElements);
    private static InspectPolicyElements _inspectPolicyElements;
    public static InspectSpolFragment InspectSpolFragment => Get(ref _inspectSpolFragment);
    private static InspectSpolFragment _inspectSpolFragment;
    public static PolicyDetail.EditPol EditPol => Get(ref _editPol);
    private static PolicyDetail.EditPol _editPol;
}
