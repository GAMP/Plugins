using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BaseLmPlugin
{
	/// <summary>
	/// Interaction logic for AddSteamKey.xaml
	/// </summary>
	public partial class AddSteamKey : UserControl
	{
		public AddSteamKey()
		{
			this.InitializeComponent();
		}

        public AddSteamKey(bool showIdField):this()
        {
            if(!showIdField)
            {
                this.idFieldDock.Visibility = System.Windows.Visibility.Collapsed;
                BindingOperations.ClearBinding(this.idField, TextBox.TextProperty);
            }           
        }
	}
}