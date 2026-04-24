// Ordered list of the 5 tutorial orders. GDD §12 locks the sequence:
// 토스트 → 샐러드 → 치즈버거 → 오믈렛 → 계란찜. No randomisation.
// GameRound reads Count / GetAt(i) to drive round progression.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DayOneChef.Gameplay.Data
{
    [CreateAssetMenu(
        menuName = "Day One Chef/Order Catalog",
        fileName = "OrderCatalog")]
    public class OrderCatalog : ScriptableObject
    {
        [SerializeField] private Order[] _orders = Array.Empty<Order>();

        public IReadOnlyList<Order> Orders => _orders;
        public int Count => _orders?.Length ?? 0;

        public Order GetAt(int index)
        {
            if (_orders == null || index < 0 || index >= _orders.Length) return null;
            return _orders[index];
        }

        public void Configure(Order[] orders)
        {
            _orders = orders ?? Array.Empty<Order>();
        }
    }
}
