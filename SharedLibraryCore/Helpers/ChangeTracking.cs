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
            Values.Add(value.GetTrackableValue());
        }

        public void ClearChanges()
        {
            Values.Clear();
        }

        public string[] GetChanges()
        {
            List<string> values = new List<string>();

            int number = 1;
            foreach (string change in Values)
            {
                values.Add($"{number} {change}");
                number++;
            }

            return values.ToArray();
        }
    }
}
