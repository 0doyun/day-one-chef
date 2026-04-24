// Fixed-sequence order queue for the tutorial run — GDD §12 locks the
// order to 토스트 → 샐러드 → 치즈버거 → 오믈렛 → 계란찜. Not randomised,
// not refillable. Intentionally a plain C# class (not MonoBehaviour) so
// the state machine can be unit-tested without a scene.

using System;
using System.Collections.Generic;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay
{
    public class OrderQueue
    {
        private readonly IReadOnlyList<Order> _orders;
        private int _index;

        public OrderQueue(IReadOnlyList<Order> orders)
        {
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _index = 0;
        }

        public int Count => _orders.Count;
        public int ProcessedCount => _index;
        public bool IsExhausted => _index >= _orders.Count;
        public Order Current => IsExhausted ? null : _orders[_index];

        /// <summary>Advance to the next order. Safe to call at the end —
        /// subsequent calls keep <see cref="IsExhausted"/> true.</summary>
        public void Advance()
        {
            if (_index < _orders.Count) _index++;
        }
    }
}
