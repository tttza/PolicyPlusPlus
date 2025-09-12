using PolicyPlusCore.Utils;
using System.Xml;

namespace PolicyPlusCore.IO
{
    public class CmtxFile
    {
        public Dictionary<string, string> Prefixes = new Dictionary<string, string>();
        public Dictionary<string, string> Comments = new Dictionary<string, string>();
        public Dictionary<string, string> Strings = new Dictionary<string, string>();
        public string SourceFile = string.Empty;

        public static CmtxFile Load(string File)
        {
            var cmtx = new CmtxFile();
            cmtx.SourceFile = File;
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(File);
            var nodeList = xmlDoc.GetElementsByTagName("policyComments");
            if (nodeList.Count == 0)
                return cmtx;
            var policyComments = nodeList[0];
            if (policyComments is null)
                return cmtx;
            foreach (XmlNode child in policyComments.ChildNodes)
            {
                switch (child.LocalName ?? "")
                {
                    case "policyNamespaces":
                        {
                            foreach (XmlNode usingElement in child.ChildNodes)
                            {
                                if (usingElement.LocalName != "using")
                                    continue;
                                var prefix = usingElement.AttributeOrNull("prefix");
                                var ns = usingElement.AttributeOrNull("namespace");
                                if (prefix is null || ns is null)
                                    continue;
                                cmtx.Prefixes.Add(prefix, ns);
                            }
                            break;
                        }
                    case "comments":
                        {
                            foreach (XmlNode admTemplateElement in child.ChildNodes)
                            {
                                if (admTemplateElement.LocalName != "admTemplate")
                                    continue;
                                foreach (XmlNode commentElement in admTemplateElement.ChildNodes)
                                {
                                    if (commentElement.LocalName != "comment")
                                        continue;
                                    var policy = commentElement.AttributeOrNull("policyRef");
                                    var text = commentElement.AttributeOrNull("commentText");
                                    if (policy is null || text is null)
                                        continue;
                                    cmtx.Comments.Add(policy, text);
                                }
                            }
                            break;
                        }
                    case "resources":
                        {
                            foreach (XmlNode stringTable in child.ChildNodes)
                            {
                                if (stringTable.LocalName != "stringTable")
                                    continue;
                                foreach (XmlNode stringElement in stringTable.ChildNodes)
                                {
                                    if (stringElement.LocalName != "string")
                                        continue;
                                    var id = stringElement.AttributeOrNull("id");
                                    if (id is null)
                                        continue;
                                    string text = stringElement.InnerText ?? string.Empty;
                                    cmtx.Strings.Add(id, text);
                                }
                            }
                            break;
                        }
                }
            }
            return cmtx;
        }

        public static CmtxFile FromCommentTable(Dictionary<string, string> Table)
        {
            var cmtx = new CmtxFile();
            int resNum = 0;
            var revPrefixes = new Dictionary<string, string>();
            foreach (var kv in Table)
            {
                var idParts = kv.Key.Split(new[] { ':' }, 2);
                if (idParts.Length != 2)
                    continue;
                var nsPart = idParts[0];
                var policyPart = idParts[1];
                if (!revPrefixes.ContainsKey(nsPart))
                {
                    string prefixId = nsPart.Replace('.', '_') + "__" + resNum;
                    revPrefixes.Add(nsPart, prefixId);
                    cmtx.Prefixes.Add(prefixId, nsPart);
                }
                string resourceId = nsPart.Replace('.', '_') + "__" + policyPart + "__" + resNum;
                cmtx.Strings.Add(resourceId, kv.Value);
                cmtx.Comments.Add(revPrefixes[nsPart] + ":" + policyPart, "$(resource." + resourceId + ")");
                resNum += 1;
            }
            return cmtx;
        }

        public Dictionary<string, string> ToCommentTable()
        {
            var commentTable = new Dictionary<string, string>();
            foreach (var comment in Comments)
            {
                var refParts = comment.Key.Split(new[] { ':' }, 2);
                if (refParts.Length != 2)
                    continue;
                var prefixKey = refParts[0];
                if (!Prefixes.TryGetValue(prefixKey, out var polNamespace))
                    continue;
                string stringRef = comment.Value ?? string.Empty;
                if (stringRef.Length < 13 || !stringRef.StartsWith("$(resource.") || !stringRef.EndsWith(")"))
                    continue;
                string stringId = stringRef.Substring(11, stringRef.Length - 12);
                if (!Strings.TryGetValue(stringId, out var resolved))
                    continue;
                commentTable.Add(polNamespace + ":" + refParts[1], resolved);
            }
            return commentTable;
        }

        public void Save()
        {
            Save(SourceFile);
        }

        public void Save(string File)
        {
            var xml = new XmlDocument();
            var declaration = xml.CreateXmlDeclaration("1.0", "utf-8", "");
            xml.AppendChild(declaration);
            var policyComments = xml.CreateElement("policyComments");
            policyComments.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
            policyComments.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            policyComments.SetAttribute("revision", "1.0");
            policyComments.SetAttribute("schemaVersion", "1.0");
            policyComments.SetAttribute("xmlns", "http://www.microsoft.com/GroupPolicy/CommentDefinitions");
            var policyNamespaces = xml.CreateElement("policyNamespaces");
            foreach (var prefix in Prefixes)
            {
                var usingElem = xml.CreateElement("using");
                usingElem.SetAttribute("prefix", prefix.Key);
                usingElem.SetAttribute("namespace", prefix.Value);
                policyNamespaces.AppendChild(usingElem);
            }
            policyComments.AppendChild(policyNamespaces);
            var commentsElem = xml.CreateElement("comments");
            var admTemplate = xml.CreateElement("admTemplate");
            foreach (var comment in Comments)
            {
                var commentElem = xml.CreateElement("comment");
                commentElem.SetAttribute("policyRef", comment.Key);
                commentElem.SetAttribute("commentText", comment.Value);
                admTemplate.AppendChild(commentElem);
            }
            commentsElem.AppendChild(admTemplate);
            policyComments.AppendChild(commentsElem);
            var resources = xml.CreateElement("resources");
            resources.SetAttribute("minRequiredRevision", "1.0");
            var stringTable = xml.CreateElement("stringTable");
            foreach (var textval in Strings)
            {
                var stringElem = xml.CreateElement("string");
                stringElem.SetAttribute("id", textval.Key);
                stringElem.InnerText = textval.Value;
                stringTable.AppendChild(stringElem);
            }
            resources.AppendChild(stringTable);
            policyComments.AppendChild(resources);
            xml.AppendChild(policyComments);
            xml.Save(File);
        }
    }
}