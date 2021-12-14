using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill.Services
{
    public class SpeechService
    {
        private SpeechSynthesizer _synth;

        public SpeechService()
        {
            _synth = new SpeechSynthesizer();
        }

        public void ParseStream(MemoryStream audioStream)
        {
            using (var recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-NZ")))
            {       

  
                recognizer.LoadGrammar(new Grammar(new GrammarBuilder("brother man bill")));
                recognizer.LoadGrammar(new Grammar(new GrammarBuilder("apple")));
                recognizer.LoadGrammar(new Grammar(new GrammarBuilder("play")));

                // Create and load a dictation grammar.  this seems to be all the normal words
                recognizer.LoadGrammar(new DictationGrammar());

                // Add a handler for the speech recognized event.  
                recognizer.SpeechRecognized += recognizer_SpeechRecognized;
                recognizer.SpeechRecognitionRejected += recognizer_SpeechRecognitionRejected;
                recognizer.SpeechHypothesized += recognizer_SpeechHypothesized;
                recognizer.SpeechDetected += recognizer_SpeechDetected;

                // Configure input to the speech recognizer.  
                // found this out by opening the dumped file and opening VLC to see codec information for the file when playing
                recognizer.SetInputToAudioStream(audioStream, new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Eight, AudioChannel.Stereo));        

                Console.WriteLine("detecting text in stream");
         
                var result = recognizer.Recognize();

                Console.WriteLine($"Recognize result:{result?.Text}");

                // Keep the console window open.  
                //while (true)
                //{
                //    Console.ReadLine();
                //}
            }
        }
        static void recognizer_SpeechDetected(object sender, SpeechDetectedEventArgs e)
        {
            Console.WriteLine("Detected speech: ");
        }

        static void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Console.WriteLine("Recognized text: " + e.Result.Text);
        }

        static void recognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (e.Result.Alternates.Count == 0)
            {
                Console.WriteLine("Speech rejected. No candidate phrases found.");
                return;
            }
            Console.WriteLine("Speech rejected. Did you mean:");
            foreach (RecognizedPhrase r in e.Result.Alternates)
            {
                Console.WriteLine("    " + r.Text);
            }
        }

        static void recognizer_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Console.WriteLine("Hypothesized text: " + e.Result.Text);
        }
    }
}
