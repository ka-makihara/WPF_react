using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfApp1_cmd
{
    public class BindableRichTextBox : RichTextBox
    {
            #region 依存関係プロパティ
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register("Document", typeof(FlowDocument), typeof(BindableRichTextBox), new UIPropertyMetadata(null, OnRichTextItemsChanged));
    #endregion  // 依存関係プロパティ
 
    #region 公開プロパティ
    public new FlowDocument Document
    {
        get { return (FlowDocument)GetValue(DocumentProperty); }
        set { SetValue(DocumentProperty, value); }
    }
    #endregion  // 公開プロパティ
 
    #region イベントハンドラ
    private static void OnRichTextItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var control = sender as RichTextBox;
        if (control != null)
        {
            control.Document = e.NewValue as FlowDocument;
        }
    }
    #endregion  // イベントハンドラ
    }

    public class RichTextItem
    {
        public string Text { get; set; }
        /*
        public Brush Foreground { get; set; }
        public FontWeight FontWeight { get; set; }
        public Thickness Margin { get; set; }
        */
        public Color Color { get; set; }
    }

    public class RichTextItemsToDocumentConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                PagePadding = new Thickness(5)
            };

            if (value is string logText)
            {
                foreach (var line in logText.Split('\n'))
                {
                    var paragraph = new Paragraph(new Run(line.TrimEnd('\r')))
                    {
                        Margin = new Thickness(0)
                    };
                    paragraph.SetResourceReference(TextElement.ForegroundProperty, GetLogBrushKey(line));
                    doc.Blocks.Add(paragraph);
                }

                return doc;
            }

            if (value is not ICollection<RichTextItem> items)
            {
                return doc;
            }

            foreach (var item in items)
            {
                doc.Blocks.Add(new Paragraph(new Run(item.Text))
                {
                    Foreground = new SolidColorBrush(item.Color),
                    FontStyle = FontStyles.Italic
                });
            }

            return doc;
        }

        private static string GetLogBrushKey(string line)
        {
            if (line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)
                || line.Contains("=NG", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Error", StringComparison.OrdinalIgnoreCase))
            {
                return "App.Log.ErrorBrush";
            }

            if (line.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase)
                || line.Contains(" Warning", StringComparison.OrdinalIgnoreCase))
            {
                return "App.Log.WarningBrush";
            }

            if (line.Contains("=OK:", StringComparison.OrdinalIgnoreCase)
                || line.Contains(" Success", StringComparison.OrdinalIgnoreCase))
            {
                return "App.Log.SuccessBrush";
            }

            return "App.Log.TextBrush";
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
