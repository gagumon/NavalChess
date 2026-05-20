using Microsoft.JSInterop;

namespace NavalChess.Screen
{    public class ScreenData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int AvailWidth { get; set; }
        public int AvailHeight { get; set; }
        public double DevicePixelRatio { get; set; }
    }

    public class ScreenService
    {
        private readonly IJSRuntime js;

        public ScreenService(IJSRuntime js)
        {
            this.js = js;
        }

        public async Task<ScreenData> GetScreenResolutionAsync()
        {
            return await js.InvokeAsync<ScreenData>("screenInfo.getScreenResolution");
        }
    }
}
