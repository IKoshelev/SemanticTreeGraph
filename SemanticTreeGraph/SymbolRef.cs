using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace SemanticTreeGraph
{
    public class SymbolRef
    {
        public SymbolRef(ISymbol originalSymbol, Document document, Location location)
        {
            OriginalSymbol = originalSymbol;
            RefDocument = document;
            RefLocation = location;
        }

        public SymbolRef(ISymbol originalSymbol, Document refDocument, Location refLocation, ISymbol refSymbol)
        {
            OriginalSymbol = originalSymbol;
            RefDocument = refDocument;
            RefLocation = refLocation;
            RefSymbol = refSymbol;
        }

        public ISymbol OriginalSymbol { get; set; }
        public ISymbol RefSymbol { get; set; }
        public Document RefDocument { get; set; }
        public Location RefLocation { get; set; }

        public override bool Equals(object obj)
        {
            var @ref = obj as SymbolRef;
            return @ref != null &&
                   EqualityComparer<ISymbol>.Default.Equals(OriginalSymbol, @ref.OriginalSymbol) &&
                   EqualityComparer<ISymbol>.Default.Equals(RefSymbol, @ref.RefSymbol);
        }

        public override int GetHashCode()
        {
            var hashCode = -1513561957;
            hashCode = hashCode * -1521134295 + EqualityComparer<ISymbol>.Default.GetHashCode(OriginalSymbol);
            hashCode = hashCode * -1521134295 + EqualityComparer<ISymbol>.Default.GetHashCode(RefSymbol);
            return hashCode;
        }
    }
}
