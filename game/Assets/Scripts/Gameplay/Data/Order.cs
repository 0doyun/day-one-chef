// Order — a single customer's request: the recipe, the speaker, and
// the customer's mood sprite. 1 round = 1 order per GDD §1.2.

using UnityEngine;

namespace DayOneChef.Gameplay.Data
{
    [CreateAssetMenu(
        menuName = "Day One Chef/Order",
        fileName = "Order")]
    public class Order : ScriptableObject
    {
        [SerializeField] private string _orderId;
        [SerializeField] private Recipe _recipe;
        [SerializeField] private CustomerMood _customerMood = CustomerMood.Waiting;
        [SerializeField, TextArea(2, 4)] private string _exampleInstruction;

        public string OrderId => _orderId;
        public Recipe Recipe => _recipe;
        public CustomerMood CustomerMood => _customerMood;
        public string ExampleInstruction => _exampleInstruction;

        public void Configure(string orderId, Recipe recipe, CustomerMood mood, string exampleInstruction)
        {
            _orderId = orderId;
            _recipe = recipe;
            _customerMood = mood;
            _exampleInstruction = exampleInstruction;
        }
    }
}
