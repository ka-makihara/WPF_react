using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1_cmd.Behavior
{
	public  static class ScrollViewerBehavior
	{
		public static double GetVerticalOffset(DependencyObject obj)
		{
			return (double)obj.GetValue(VerticalOffsetProperty);
		}

		public static void SetVerticalOffset(DependencyObject obj, double value)
		{
			obj.SetValue(VerticalOffsetProperty, value);
		}

		public static readonly DependencyProperty VerticalOffsetProperty =
		 DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ScrollViewerBehavior), new UIPropertyMetadata(0.0, OnVerticalOffsetChanged));

		private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
		{
			ScrollViewer scrollViewer = target as ScrollViewer;
			if (scrollViewer != null)
			{
				scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
			}
		}
	}
}

