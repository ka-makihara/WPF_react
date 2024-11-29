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
            /*
            if( !(value is RichTextItem item))
            {
                return Binding.DoNothing;
            }
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run()
            {
                Text = item.Text,
                Foreground = new SolidColorBrush(item.Color),
                FontStyle = FontStyles.Italic,
            });
            return new FlowDocument(paragraph);
            */
            
            var doc = new FlowDocument();
     
            foreach (var item in value as ICollection<RichTextItem>)
            {
                var paragraph = new Paragraph(new Run(item.Text))
                {
                    Foreground = new SolidColorBrush(item.Color),
                    FontStyle = FontStyles.Italic,
                    //FontWeight = item.FontWeight,
                    //Margin = item.Margin,
                };
                doc.Blocks.Add(paragraph);
            }
            return doc;
        }
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
