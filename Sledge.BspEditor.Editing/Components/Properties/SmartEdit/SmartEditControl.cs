using System.Collections.Generic;
using System.Windows.Forms;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.DataStructures.GameData;

namespace Sledge.BspEditor.Editing.Components.Properties.SmartEdit
{
    public abstract class SmartEditControl : FlowLayoutPanel, IObjectPropertyEditor
    {
        public Control Control => this;

        public string OriginalName { get; private set; }
        public string PropertyName { get; private set; }
        public string PropertyValue { get; private set; }
        public Property Property { get; private set; }
        public abstract string PriorityHint { get; }

        public delegate void ValueChangedEventHandler(object sender, string propertyName, string propertyValue);
        public delegate void NameChangedEventHandler(object sender, string oldName, string newName);

        public event ValueChangedEventHandler ValueChanged;
        public event ValueChangedEventHandler NameChanged;

        protected virtual void OnValueChanged()
        {
            if (_setting) return;
            PropertyValue = GetValue();
            ValueChanged?.Invoke(this, PropertyName, PropertyValue);
        }
        
        protected virtual void OnNameChanged()
        {
            if (_setting) return;
            PropertyName = GetName();
            NameChanged?.Invoke(this, OriginalName, PropertyName);
        }

        protected SmartEditControl()
        {
            Dock = DockStyle.Fill;
        }

        private bool _setting;

        public void SetProperty(string originalName, string newName, string currentValue, Property property)
        {
            _setting = true;
            OriginalName = originalName;
            PropertyName = newName;
            PropertyValue = currentValue;
            Property = property;
            OnSetProperty();
            _setting = false;
        }

        public abstract bool SupportsType(VariableType type);
        protected abstract string GetName();
        protected abstract string GetValue();
        protected abstract void OnSetProperty();
    }
}