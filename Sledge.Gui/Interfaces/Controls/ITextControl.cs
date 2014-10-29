using System;

namespace Sledge.Gui.Interfaces.Controls
{
    public interface ITextControl : IControl
    {
        string TextKey { get; set; }
        string Text { get; set; }
        int FontSize { get; }
        bool Bold { get; set; }
        bool Italic { get; set; }

        event EventHandler TextChanged;
    }
}