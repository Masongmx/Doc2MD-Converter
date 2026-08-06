using System.Windows;
using Doc2MD.Models;

namespace Doc2MD.Services;

/// <summary>
/// Toast 消息管理服务，负责显示、覆盖和自动隐藏 Toast 通知
/// </summary>
public sealed class ToastService
{
    private CancellationTokenSource? _toastCts;

    public string Message { get; private set; } = string.Empty;
    public string Tone { get; private set; } = "info";
    public bool IsVisible { get; private set; }

    public event Action? Changed;

    public void Show(string message, ToastType type = ToastType.Info)
    {
        _toastCts?.Cancel();
        _toastCts?.Dispose();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        Message = message;
        Tone = type switch
        {
            ToastType.Success => "success",
            ToastType.Warning => "warning",
            ToastType.Error => "error",
            _ => "info"
        };
        IsVisible = true;
        Changed?.Invoke();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2400, token);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        IsVisible = false;
                        Changed?.Invoke();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Suppressed by newer toast.
            }
        }, token);
    }
}
