using Microsoft.Maui.Controls;
using ThreatIntelClient.Models;

namespace ThreatIntelClient.Views;

public class FeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate CveTemplate { get; set; }
    public DataTemplate NewsTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is Cve)
            return CveTemplate;
        if (item is NewsArticle)
            return NewsTemplate;

        return null;
    }
}
