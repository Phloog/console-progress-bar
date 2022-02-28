using System;
using System.Linq;
using System.Text;
using System.Threading;

namespace AaronLuna.ConsoleProgressBar
{
    public class ConsoleProgressBar : IDisposable, IProgress<double>
    {
        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 8);

        private string _currentText = string.Empty;
        internal int AnimationIndex;
        internal double CurrentProgress;
        internal bool Disposed;

        internal Timer Timer;

        internal DateTime StartTime;

        public ConsoleProgressBar()
        {
            Console.OutputEncoding = Encoding.UTF8;

            NumberOfBlocks = 10;
            StartBracket = "[";
            EndBracket = "]";
            CompletedBlock = "#";
            IncompleteBlock = "-";
            AnimationSequence = ProgressAnimations.Default;

            DisplayBar = true;
            DisplayPercentComplete = true;
            DisplayAnimation = true;

            Column = Console.CursorLeft;

            Timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected) ResetTimer();
        }

        public bool RedrawWholeBar { get; set; }
        public int Column { get; set; }

        public int NumberOfBlocks { get; set; }
        public string StartBracket { get; set; }
        public string EndBracket { get; set; }
        public string CompletedBlock { get; set; }
        public string IncompleteBlock { get; set; }
        public string AnimationSequence { get; set; }
        public bool DisplayBar { get; set; }
        public bool DisplayPercentComplete { get; set; }
        public bool DisplayRunTime { get; set; }
        public bool DisplayETA { get; set; }
        public bool DisplayAnimation { get; set; }

        public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ConsoleProgressBar Start(bool inline = true)
        {
            if (inline)
                Column = Console.CursorLeft;

            return this;
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref CurrentProgress, value);

            if (value < .001 || value > .999)
                StartTime = DateTime.Now;
        }

        private void TimerHandler(object state)
        {
            lock (Timer)
            {
                if (Disposed) return;
                UpdateText(GetProgressBarText(CurrentProgress));
                ResetTimer();
            }
        }

        private string GetProgressBarText(double currentProgress)
        {
            const string singleSpace = " ";

            var numBlocksCompleted = (int) (currentProgress * NumberOfBlocks);

            var completedBlocks =
                Enumerable.Range(0, numBlocksCompleted).Aggregate(
                    string.Empty,
                    (current, _) => current + CompletedBlock);

            var incompleteBlocks =
                Enumerable.Range(0, NumberOfBlocks - numBlocksCompleted).Aggregate(
                    string.Empty,
                    (current, _) => current + IncompleteBlock);

            var progressBar =
                $"{StartBracket}{completedBlocks}{incompleteBlocks}{EndBracket}";
            var percent = $"{currentProgress:P0}".PadLeft(4, '\u00a0');

            var runtimeTimespan = DateTime.Now - StartTime;
            var runtime = string.Empty;
            if (DisplayRunTime && currentProgress > 0 && currentProgress < 1)
            {
                runtime = $"{runtimeTimespan:h\\:mm\\:ss\\.f}";
                if (runtime.StartsWith("00:")) runtime = runtime.Substring(3);
                if (runtime.StartsWith("0:")) runtime = runtime.Substring(2);
                runtime = singleSpace + runtime;
            }

            var eta = string.Empty;
            if (DisplayETA && currentProgress > 0 && currentProgress < 1)
            {
                var secondsPerPercent = currentProgress > 0 ? runtimeTimespan.TotalSeconds / currentProgress : 0;
                var estimate = new TimeSpan(0, 0, (int)((1.0 - currentProgress) * secondsPerPercent));
                eta = $"{estimate:h\\:mm\\:ss}";
                if (eta.StartsWith("00:")) eta = eta.Substring(3);
                if (eta.StartsWith("0:")) eta = eta.Substring(2);
                eta = $" ({eta} left)";
            }
            
            var animationFrame =
                AnimationSequence[AnimationIndex++ % AnimationSequence.Length];
            var animation = $"{animationFrame}";

            progressBar = DisplayBar
                ? progressBar + singleSpace
                : string.Empty;

            percent = DisplayPercentComplete
                ? percent + singleSpace
                : string.Empty;

            if (!DisplayAnimation || currentProgress is 1)
            {
                animation = string.Empty;
            }

            return (progressBar + percent + animation + runtime + eta).TrimEnd();
        }

        internal void UpdateText(string text)
        {
            // Get length of common portion
            var commonPrefixLength = 0;
            var commonLength = Math.Min(_currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] ==
                   _currentText[commonPrefixLength])
                commonPrefixLength++;

            // Backtrack to the first differing character
            var outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', _currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            var overlapCount = _currentText.Length - text.Length;

            var oldcolor = Console.ForegroundColor;
            Console.ForegroundColor = ForegroundColor;
            
            if (RedrawWholeBar)
            {
                Console.SetCursorPosition(Column, Console.CursorTop);
                Console.Write(text);

                if (overlapCount > 0)
                {
                    Console.Write(new string(' ', overlapCount));
                    Console.Write(new string('\b', overlapCount));
                }

            }
            else
            {
                if (overlapCount > 0)
                {
                    outputBuilder.Append(' ', overlapCount);
                    outputBuilder.Append('\b', overlapCount);
                }

                Console.Write(outputBuilder);
            }

            Console.ForegroundColor = oldcolor;
            _currentText = text;
        }

        internal void ResetTimer()
        {
            Timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            lock (Timer)
            {
                Disposed = true;
            }
        }
    }
}