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
using System.Windows.Media.Animation;

namespace FanDemo
{
    /// <summary>
    /// Interaction logic for Favourites.xaml
    /// </summary>

    public partial class Favourites : Canvas
    {
        public Favourites()
        {
            InitializeComponent();
            this.close.MouseLeftButtonUp += new MouseButtonEventHandler(close_MouseLeftButtonUp);
            this.detailSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(detailSlider_ValueChanged);
        }

        FanPanel fan;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            fan = (FanPanel)sender;
            fan.Refresh += new RoutedEventHandler(fan_Refresh);
            UpdateCount();
        }

        void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (!fan.IsWrapPanel)
            {
                this.grid.Width = 660;
                this.grid.Height = 550;
                this.text.Visibility = Visibility.Collapsed;
                fan.IsWrapPanel = true;
                this.BeginStoryboard((Storyboard)grid.FindResource("expandPanel"));
            }
        }

        void close_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
        }

        public void Hide()
        {
            fan.IsWrapPanel = false;
            detailSlider.Value = detailSlider.Maximum;
            this.BeginStoryboard((Storyboard)grid.FindResource("collapsePanel"));
        }

        void OnCompleted(object sender, RoutedEventArgs e)
        {
            if (!fan.IsWrapPanel && this.grid.Width != 230)
            {
                this.grid.Width = 230;
                this.grid.Height = 210;
                this.text.Visibility = Visibility.Visible;
                modal.Visibility = Visibility.Collapsed;
            }
        }

        void detailSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int n = (int)(detailSlider.Value * 100 / detailSlider.Maximum);
            textDetail.Text = String.Format("DETAIL LEVEL: {0}%", n);
        }

        void fan_Refresh(object sender, RoutedEventArgs e)
        {
            UpdateCount();
        }

        void UpdateCount()
        {
            counter.Text = fan.Children.Count.ToString();
        }

        void OnItemClick(object sender, RoutedEventArgs e)
        {
            XmlDataProvider cvs = (XmlDataProvider)FindResource("Things");
            cvs.Document.ChildNodes[0].RemoveChild(cvs.Document.ChildNodes[0].ChildNodes[0]);
            //someFavourites.Items.Remove(sender);
            e.Handled = true;
        }
    }
}