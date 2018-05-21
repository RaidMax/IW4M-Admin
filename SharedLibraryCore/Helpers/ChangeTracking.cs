using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Helpers
{
    public class ChangeTracking
    {
        List<string> Values;

        public ChangeTracking()
        {
            Values = new List<string>();
        }

        public void OnChange(ITrackable value)
        {
            if (Values.Count > 30)
                Values.RemoveAt(0);
            Values.Add($"{DateTime.Now.ToString("HH:mm:ss.fff")} {value.GetTrackableValue()}");
        }

        public void ClearChanges()
        {
            Values.Clear();
        }

        public string[] GetChanges() => Values.ToArray();
    }
}
