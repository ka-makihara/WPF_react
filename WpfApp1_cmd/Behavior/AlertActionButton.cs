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
	internal class AlertActionButton : TriggerAction<Button>
	{
		#region メッセージプロパティ
		public string Message
		{
			get { return (string)GetValue(MessageProperty); }
			set { SetValue(MessageProperty, value); }
		}

		public static readonly DependencyProperty MessageProperty =
			DependencyProperty.Register("Message", typeof(string), typeof(AlertActionButton), new UIPropertyMetadata(null));
		#endregion

		public AlertActionButton()
		{
		}

		// Actionが実行されたときに呼び出される
		protected override void Invoke(object parameter)
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
	<i:Interaction.Triggers>
		<i:EventTrigger EventName="Click">
			<local:AlertAction Message="こんにちはアクション"/>
		</i:EventTrigger>
	</i:Interaction.Triggers>
</Button>
*/

