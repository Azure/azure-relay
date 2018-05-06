// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PortBridgeClientAgent
{
    using System.Configuration;

    public class FirewallRuleElement : ConfigurationElement
    {
        internal const string sourceRangeBeginString = "sourceRangeBegin";
        internal const string sourceRangeEndString = "sourceRangeEnd";
        internal const string sourceString = "source";

        public FirewallRuleElement(string sourceRangeBegin, string sourceRangeEnd)
        {
            SourceRangeBegin = sourceRangeBegin;
            SourceRangeEnd = sourceRangeEnd;
        }

        public FirewallRuleElement(string source)
        {
            Source = source;
        }

        public FirewallRuleElement()
        {
        }

        [ConfigurationProperty(sourceRangeBeginString, IsRequired = false)]
        [RegexStringValidator(@"^$|^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$")]
        public string SourceRangeBegin
        {
            get { return (string) this[sourceRangeBeginString]; }
            set { this[sourceRangeBeginString] = value; }
        }

        [ConfigurationProperty(sourceRangeEndString, IsRequired = false)]
        [RegexStringValidator(@"^$|^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$")]
        public string SourceRangeEnd
        {
            get { return (string) this[sourceRangeEndString]; }
            set { this[sourceRangeEndString] = value; }
        }

        [ConfigurationProperty(sourceString, IsRequired = false)]
        [RegexStringValidator(@"^$|^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$")]
        public string Source
        {
            get { return (string) this[sourceString]; }
            set { this[sourceString] = value; }
        }
    }
}