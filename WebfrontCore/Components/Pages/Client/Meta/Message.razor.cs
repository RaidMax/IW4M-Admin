using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos.Meta.Responses;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.Client.Meta;

public partial class Message
{
    [Parameter] public MessageResponse Meta { get; set; }
    [Parameter] public bool ShowHeader { get; set; } = true;
    [Inject] public IWebfrontApiClient Api { get; set; }
    private bool IsOpen { get; set; }
    private bool IsLoading { get; set; }
    private List<MessageResponse> ContextMessages { get; set; }

    private async Task ToggleContext()
    {
        IsOpen = !IsOpen;
        if (IsOpen && ContextMessages == null)
        {
            IsLoading = true;
            try
            {
                // Passing long time directly might have precision issues with JSON, but API expects long.
                // Meta.When is DateTime. We need ToFileTimeUtc if that's what backend expects.
                // Backend Controller expected 'long when'.
                ContextMessages = await Api.GetMessageContextAsync(Meta.ServerId.ToString(), Meta.When.ToFileTimeUtc());
            }
            catch (Exception ex)
            {
                // Handle error
                System.Console.WriteLine(ex);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
