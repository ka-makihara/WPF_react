using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1_cmd.Behavior
{
	internal class AlertBehaviorButton : Behavior<Button>
	{
		#region メッセージプロパティ
		public string Message
		{
			get { return (string)GetValue(MessageProperty); }
			set { SetValue(MessageProperty, value); }
		}
		// Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MessageProperty =
			DependencyProperty.Register("Message", typeof(string), typeof(AlertBehaviorButton), new UIPropertyMetadata(null));
		#endregion

		public AlertBehaviorButton()
		{
		}

		//要素にアタッチされたときに呼び出される(イベントハンドラの登録)
		protected override void OnAttached()
		{
			base.OnAttached();
			AssociatedObject.Click += Alert;
		}

		//要素からデタッチされたときに呼び出される(イベントハンドラの解除)
		protected override void OnDetaching()
		{
			base.OnDetaching();
			AssociatedObject.Click -= Alert;
		}

		//メッセージが入力されている場合、メッセージを表示する
		private void Alert(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrEmpty(Message))
			{
				MessageBox.Show(Message);
			}
		}
	}
}

/* XAML使用例
 <Button Content="Button" HorizontalAlignment="Left" Margin="8,8,0,0" VerticalAlignment="Top" Width="75">
	<i:Interaction.Behaviors>
		<local:AlertBehavior Message="こんにちは世界"/>
	</i:Interaction.Behaviors>
*/
