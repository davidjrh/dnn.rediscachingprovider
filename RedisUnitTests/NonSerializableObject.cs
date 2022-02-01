using System;

namespace RedisUnitTests
{
    [Serializable]
    public class NonSerializableClass
    {
        public Item Item = new Item();
    }

    public class Item
    {
        public object Name;// = "Item name";
    }
}
