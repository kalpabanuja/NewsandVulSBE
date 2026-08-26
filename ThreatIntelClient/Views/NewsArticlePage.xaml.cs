using ThreatIntelClient.Models;

namespace ThreatIntelClient.Views;

public partial class NewsArticlePage : ContentPage
{
    public NewsArticlePage(NewsArticle article)
    {
        InitializeComponent();
        BindingContext = article;
    }
}
