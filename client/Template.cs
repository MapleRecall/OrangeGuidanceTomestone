// using System.Text;
//
// namespace OrangeGuidanceTomestone;
//
// internal class Template {
//     internal static readonly Template[] All = {
//         // new(new []{})
//     };
//
//     internal IReadOnlyList<TemplatePart> Parts { get; }
//     // internal IReadOnlyList<string> Conjunctions { get; }
//     internal uint Variables { get; }
//
//
//     internal Template(IReadOnlyList<TemplatePart> parts) {
//         this.Parts = parts;
//         // this.Conjunctions = conjunctions;
//
//         // if (this.Parts.Count - 1 != this.Conjunctions.Count) {
//             // throw new ArgumentException("should have one less conjunction than parts");
//         // }
//         
//         this.Variables = (uint) this.Parts.Select(part => (int) part.Variables).Sum();
//     }
//
//     internal string Format(params object[] variables) {
//         var result = new StringBuilder();
//         var varIdx = 0;
//         for (var i = 0; i < this.Parts.Count; i++) {
//             var part = this.Parts[i];
//             result.Append(part.Format(variables[varIdx..(varIdx + (int) part.Variables)]));
//             varIdx += (int) part.Variables;
//
//             // if (i == this.Parts.Count - 1) {
//                 // continue;
//             // }
//
//             // var conj = this.Conjunctions[i - 1];
//             // result.Append(conj)
//         }
//
//         return result.ToString();
//     }
// }
//
// internal class TemplatePart {
//     internal string Template { get; }
//     internal uint Variables { get; }
//
//     internal TemplatePart(string template, uint variables) {
//         this.Template = template;
//         var count = 0;
//         var lastIndex = -1;
//         while (true) {
//             lastIndex = this.Template.IndexOf("%s", lastIndex + 1, StringComparison.Ordinal);
//             if (lastIndex == -1) {
//                 break;
//             }
//
//             count += 1;
//         }
//
//         this.Variables = count;
//     }
//
//     internal string Format(params object[] variables) {
//         return string.Format(this.Template, variables);
//     }
// }
