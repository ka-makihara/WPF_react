using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd.View
{
    /// <summary>
    /// BView.xaml の相互作用ロジック
    /// </summary>
    public partial class BView : UserControl
    {
        public BView()
        {
            InitializeComponent();
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(BView),
                new FrameworkPropertyMetadata("項目名"));
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(BView),
                new FrameworkPropertyMetadata("値",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        /*
        // 依存関係プロパティの定義に必要なDependencyProperty(ボイラープレート)
        // わかりやすくするため名前は プロパティ名+Property にする
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),        // プロパティ名
                typeof(MyCtrlData),   // プロパティの型
                typeof(BView),        // プロパティを所有する型(自分自身の型)
                new PropertyMetadata(   // 初期値をPropertyMetadataで指定
                    false)
                );

        // 実際にバインドするプロパティ
        // 上で定義したDependencyPropertyをラップするCLRプロパティ
        public MyCtrlData Value
        {
            get => (MyCtrlData)GetValue(ValueProperty); 
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty SelectedTextProperty =
            DependencyProperty.Register(
                nameof(SelectedText),
                typeof(string),
                typeof(BView),
                new PropertyMetadata(
                    false)
                );
        public string SelectedText
        {
            get => (string)GetValue(SelectedTextProperty);
            set => SetValue(SelectedTextProperty, value);
        }
        */
    }
}
