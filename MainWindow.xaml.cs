using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace DiscordWebhookUtility
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            BeginAnimation(OpacityProperty, animation);
        }

        private async void SpamWebhook_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs()) return;

            string webhookUrl = WebhookUrlInput.Text;
            string message = MessageInput.Text;
            int count = int.Parse(CountInput.Text);
            float delay = float.Parse(DelayInput.Text);
            string imageUrl = ImageUrlInput.Text;

            await SpamWebhookAsync(webhookUrl, message, count, delay, imageUrl);
        }

        private async void DeleteWebhook_Click(object sender, RoutedEventArgs e)
        {
            string webhookUrl = WebhookUrlInput.Text;
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                UpdateStatus("Please enter a webhook URL.");
                return;
            }
            await DeleteWebhookAsync(webhookUrl);
        }

        private async void GetWebhookInfo_Click(object sender, RoutedEventArgs e)
        {
            string webhookUrl = WebhookUrlInput.Text;
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                UpdateStatus("Please enter a webhook URL.");
                return;
            }
            await GetWebhookInfoAsync(webhookUrl);
        }

        private void ShowHint_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string hint = GetHintText(button?.Tag?.ToString() ?? "");
            MessageBox.Show(hint, "Hint", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/ZoniBoy00",
                UseShellExecute = true
            });
        }

        private static string GetHintText(string tag) => tag switch
        {
            "WebhookUrl" => "Enter the Discord webhook URL. It should start with https://discord.com/api/webhooks/ or https://discordapp.com/api/webhooks/",
            "Message" => "Enter the message you want to send to the webhook.",
            "Count" => "Enter the number of times you want to send the message. Must be a positive integer.",
            "Delay" => "Enter the delay between messages in seconds. Must be at least 1 second to avoid rate limiting.",
            "ImageUrl" => "Optionally, enter a URL of an image or GIF to attach to the message. The URL must end with .jpg, .png, or .gif",
            _ => "No hint available."
        };

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(WebhookUrlInput.Text))
            {
                UpdateStatus("Please enter a webhook URL.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                UpdateStatus("Please enter a message.");
                return false;
            }
            if (!int.TryParse(CountInput.Text, out int count) || count <= 0)
            {
                UpdateStatus("Invalid count. Please enter a positive integer.");
                return false;
            }
            if (!float.TryParse(DelayInput.Text, out float delay) || delay < 1)
            {
                UpdateStatus("Invalid delay. Please enter a number greater than or equal to 1.");
                return false;
            }
            if (!string.IsNullOrWhiteSpace(ImageUrlInput.Text))
            {
                string imageUrl = ImageUrlInput.Text.ToLower();
                if (!imageUrl.EndsWith(".jpg") && !imageUrl.EndsWith(".png") && !imageUrl.EndsWith(".gif"))
                {
                    UpdateStatus("Invalid image URL. The URL must end with .jpg, .png, or .gif");
                    return false;
                }
            }
            return true;
        }

        private async Task SpamWebhookAsync(string webhookUrl, string message, int count, float delay, string imageUrl)
        {
            UpdateStatus("Spamming webhook...");
            string? imagePath = null;

            if (!string.IsNullOrEmpty(imageUrl))
            {
                UpdateStatus("Downloading image/GIF...");
                imagePath = await DownloadImageAsync(imageUrl);
                if (string.IsNullOrEmpty(imagePath))
                {
                    UpdateStatus("Failed to download image/GIF. Skipping attachment.");
                }
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    content.Add(new StringContent(message), "content");
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        content.Add(new StreamContent(File.OpenRead(imagePath)), "file", Path.GetFileName(imagePath));
                    }

                    var response = await _httpClient.PostAsync(webhookUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        UpdateStatus($"Message {i + 1}/{count} sent successfully.");
                    }
                    else
                    {
                        UpdateStatus($"Failed to send message {i + 1}/{count}: HTTP {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error occurred: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    File.Delete(imagePath);
                    UpdateStatus("Image/GIF file deleted successfully.");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to delete image file: {ex.Message}");
                }
            }

            UpdateStatus("Webhook spam completed.");
        }

        private async Task<string?> DownloadImageAsync(string imageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    string fileName = Path.GetFileName(imageUrl);
                    string filePath = Path.Combine(Path.GetTempPath(), fileName);
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    return filePath;
                }
                else
                {
                    UpdateStatus($"Failed to download image. HTTP Status Code: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error downloading image: {ex.Message}");
                return null;
            }
        }

        private async Task DeleteWebhookAsync(string webhookUrl)
        {
            try
            {
                if (webhookUrl.StartsWith("https://discord.com/api/webhooks/") || webhookUrl.StartsWith("https://discordapp.com/api/webhooks/"))
                {
                    var response = await _httpClient.DeleteAsync(webhookUrl);
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        UpdateStatus("Webhook deleted successfully.");
                    }
                    else
                    {
                        UpdateStatus($"Failed to delete webhook. HTTP Status Code: {response.StatusCode}");
                    }
                }
                else
                {
                    UpdateStatus("Invalid webhook URL. Deletion aborted.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error deleting webhook: {ex.Message}");
            }
        }

        private async Task GetWebhookInfoAsync(string webhookUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(webhookUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    UpdateStatus($"Webhook Info:\n" +
                        $"Name: {json["name"]}\n" +
                        $"Avatar URL: https://cdn.discordapp.com/avatars/{json["id"]}/{json["avatar"]}.png\n" +
                        $"Guild ID: {json["guild_id"]}\n" +
                        $"Channel ID: {json["channel_id"]}\n" +
                        $"Webhook ID: {json["id"]}\n" +
                        $"Webhook Token: {json["token"]}");
                }
                else
                {
                    UpdateStatus("Webhook not found or unable to retrieve information.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error retrieving webhook info: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}

