namespace PolicyPlusCore.Core
{
    // These structures hold information on how the UI for policy elements appears
    public class Presentation
    {
        public string Name = string.Empty;
        public List<PresentationElement> Elements = new List<PresentationElement>();
    }

    public abstract class PresentationElement
    {
        public string ID = string.Empty; // refId
        public string ElementType = string.Empty;
    }

    public class LabelPresentationElement : PresentationElement // <text>
    {
        public string Text = string.Empty; // Inner text
    }

    public class NumericBoxPresentationElement : PresentationElement // <decimalTextBox>
    {
        public uint DefaultValue; // defaultValue
        public bool HasSpinner = true; // spin
        public uint SpinnerIncrement; // spinStep
        public string Label = string.Empty; // Inner text
    }

    public class TextBoxPresentationElement : PresentationElement // <textBox>
    {
        public string Label = string.Empty; // <label>
        public string? DefaultValue; // <defaultValue>
    }

    public class CheckBoxPresentationElement : PresentationElement // <checkBox>
    {
        public bool DefaultState; // defaultChecked
        public string Text = string.Empty; // Inner text
    }

    public class ComboBoxPresentationElement : PresentationElement // <comboBox>
    {
        public bool NoSort; // noSort
        public string Label = string.Empty; // <label>
        public string? DefaultText; // <default>
        public List<string> Suggestions = new List<string>(); // <suggestion>s
    }

    public class DropDownPresentationElement : PresentationElement // <dropdownList>
    {
        public bool NoSort; // noSort
        public int? DefaultItemID; // defaultItem
        public string Label = string.Empty; // Inner text
    }

    public class ListPresentationElement : PresentationElement // <listBox>
    {
        public string Label = string.Empty; // Inner text
    }

    public class MultiTextPresentationElement : PresentationElement // <multiTextBox>
    {
        public string Label = string.Empty; // Inner text
                                            // Undocumented, but never appears to have any other parameters
    }
}
