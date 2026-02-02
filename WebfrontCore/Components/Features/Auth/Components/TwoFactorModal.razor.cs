using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Dtos;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Auth.Components;

public partial class TwoFactorModal : ComponentBase
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required IToastService ToastService { get; set; }

    [Parameter] public int ClientId { get; set; }
    [Parameter] public bool HasTwoFactor { get; set; }
    [Parameter] public EventCallback<bool> OnChanged { get; set; }

    private bool IsLoading { get; set; }
    private TwoFactorSetupInfo? SetupInfo { get; set; }
    private string? VerifyCode { get; set; }
    private string? ErrorMessage { get; set; }

    private async Task StartSetup()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SetupInfo = await DataService.EnableTwoFactorAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ConfirmSetup()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (await DataService.ConfirmTwoFactorAsync(SetupInfo!.Secret, VerifyCode))
            {
                HasTwoFactor = true;
                SetupInfo = null;
                await ToastService.ShowSuccessAsync(AppState.Loc("WEBFRONT_2FA_ENABLED"));
                await OnChanged.InvokeAsync(true);
            }
            else
            {
                ErrorMessage = AppState.Loc("WEBFRONT_2FA_INVALID_CODE");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task Disable2FA()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await DataService.DisableTwoFactorAsync();
            HasTwoFactor = false;
            await ToastService.ShowSuccessAsync(AppState.Loc("WEBFRONT_2FA_DISABLED"));
            await OnChanged.InvokeAsync(false);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
