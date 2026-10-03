using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChatSnippets.App.Interop;

namespace ChatSnippets.App.Views;

public partial class FlagWindow : Window
{
    public event Action? Clicked;

    public FlagWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NoActivate.Apply(this);
    }

    /// <summary>E76C = ›, E76B = ‹.</summary>
    public void SetDirection(bool pointsRight) => Arrow.Text = pointsRight ? "" : "";

    public async Task FlashAsync()
    {
        Body.Background = (Brush)FindResource("Success");
        await Task.Delay(1000);
        Body.Background = (Brush)FindResource("Accent");
    }

    void Body_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Clicked?.Invoke();
}
