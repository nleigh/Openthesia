using System.Collections.Generic;

namespace Openthesia.Core.Midi
{
    public static class SongQueueManager
    {
        public static List<string> CurrentQueue { get; private set; } = new();
        public static int CurrentIndex { get; private set; } = -1;
        public static bool AutoAdvance { get; set; } = true;

        public static void SetQueue(List<string> songs, int startIndex)
        {
            CurrentQueue = (List<string>)songs;
            CurrentIndex = startIndex;
        }

        public static void AddToQueue(string filePath)
        {
            CurrentQueue.Add(filePath);
        }

        public static string GetNext()
        {
            if (CurrentIndex + 1 < CurrentQueue.Count)
            {
                CurrentIndex++;
                return CurrentQueue[CurrentIndex];
            }
            return null;
        }

        public static string GetPrevious()
        {
            if (CurrentIndex - 1 >= 0)
            {
                CurrentIndex--;
                return CurrentQueue[CurrentIndex];
            }
            return null;
        }

        public static bool HasNext => CurrentIndex + 1 < CurrentQueue.Count;
        public static bool HasPrevious => CurrentIndex - 1 >= 0;
        
        public static void Clear()
        {
            CurrentQueue.Clear();
            CurrentIndex = -1;
        }
    }
}
