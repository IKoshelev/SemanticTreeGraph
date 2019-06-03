using Microsoft.CodeAnalysis;

namespace SemanticTreeGraph
{
    public class SymbolRef
    {
        public SymbolRef(ISymbol originalSymbol, Document document, Location location)
        {
            OriginalSymbol = originalSymbol;
            Document = document;
            Location = location;
        }

        public SymbolRef(ISymbol originalSymbol, Document document, Location location, ISymbol referencingSymbol)
        {
            OriginalSymbol = originalSymbol;
            Document = document;
            Location = location;
            ReferencingSymbol = referencingSymbol;
        }

        public ISymbol OriginalSymbol { get; set; }
        public ISymbol ReferencingSymbol { get; set; }
        public Document Document { get; set; }
        public Location Location { get; set; }
    }
}
