using System;
using System.Drawing;

namespace MusicBeePlugin.Config
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigPropertyAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }

        public ConfigPropertyAttribute(string displayName = null, string description = null, string category = "General")
        {
            DisplayName = displayName;
            Description = description;
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RangeAttribute : Attribute
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }

        public RangeAttribute(double minimum, double maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ActionConfigAttribute : Attribute
    {
        public Type[] AllowedActions { get; set; }

        public ActionConfigAttribute(params Type[] allowedActions)
        {
            AllowedActions = allowedActions;
        }
    }
} 