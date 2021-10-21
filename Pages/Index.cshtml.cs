using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Speech.Synthesis;
using System.IO;
using NAudio.Wave;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;

namespace WebTTS.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public TTSInput Input { get; set; }
        public SelectList Voices { get; set; }
        public SelectList Devices { get; set; }
        public SelectList Rates { get; set; }
        private SpeechSynthesizer _speech { get; set; }
        private readonly ILogger<IndexModel> _logger;

        public static TTSInput settings;
        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
            _speech = new SpeechSynthesizer();

            if (settings == null)
            {
                try
                {
                    var disksettings = JsonConvert.DeserializeObject<TTSInput>(System.IO.File.ReadAllText("userprefs.json"));
                    settings = disksettings;
                }
                catch
                {
                    settings = new TTSInput() { Voice = _speech.GetInstalledVoices().Select(x => x.VoiceInfo.Name).First(), Device = WaveOut.GetCapabilities(0).ProductName, Rate = 3 };
                    Console.WriteLine("Failed to read persisted data, using defaults.");
                }
            }
            Rates = new SelectList(Enumerable.Range(-10, 21), Input?.Rate ?? settings.Rate);
            Voices = new SelectList(_speech.GetInstalledVoices().Select(x => x.VoiceInfo.Name), Input?.Voice ?? settings.Voice);
            Devices = new SelectList(Enumerable.Range(0, WaveOut.DeviceCount - 1).Select(x => WaveOut.GetCapabilities(x).ProductName), Input?.Device ?? settings.Device);
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!string.IsNullOrWhiteSpace(Input.Message))
            {
                using (var ms = new MemoryStream())
                {
                    _speech.SetOutputToWaveStream(ms);
                    _speech.SelectVoice(Input.Voice);
                    _speech.Speak(Input.Message);
                    ms.Position = 0;
                    using (var ws = new WaveFileReader(ms))
                    {
                        using (var wo = new WaveOutEvent() { DeviceNumber = Enumerable.Range(0, WaveOut.DeviceCount - 1).Where(x => WaveOut.GetCapabilities(x).ProductName == Input.Device).First() })
                        {
                            wo.Init(ws);
                            wo.Play();
                            while(wo.PlaybackState == PlaybackState.Playing)
                            {
                                Thread.Sleep(100);
                            }
                        }
                    }
                    _speech.SetOutputToNull();
                }

            }
            if (Input.Device != settings.Device || Input.Voice != settings.Voice || Input.Rate != settings.Rate)
            {
                Console.WriteLine("Persisting to disk due to change.");
                Input.Message = "";
                System.IO.File.WriteAllText("userprefs.json", JsonConvert.SerializeObject(Input));
            }
            return new EmptyResult();
        }
    }
    public class TTSInput
    {
        public string Message { get; set; }
        public string Voice { get; set; }
        public string Device { get; set; }
        public int Rate { get; set; }
    }
}
