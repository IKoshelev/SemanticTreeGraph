using Microsoft.CodeAnalysis;

namespace SemanticTreeGraph
{
    public class SymbolRef
    {
        public SymbolRef(ISymbol symbol, Microsoft.CodeAnalysis.Document document, Location location)
        {
            Symbol = symbol;
            Document = document;
            Location = location;
        }

        public ISymbol Symbol { get; set; }
        public Document Document { get; set; }
        public Location Location { get; set; }
    }
}
