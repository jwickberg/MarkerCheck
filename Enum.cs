using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace MarkerCheck
{
    /// <summary>
    /// <para>Represents a serialized value that should be an enumeration, but because of forward-compatibility concerns
    /// an enumeration would not work. This class allows most of the type-safety you get from an enumeration while
    /// still allowing for forward-compatibility by allowing values not yet defined. Serialization and deserialization 
    /// occur as if an enumeration was used.</para>
    /// <para>To use, create a class to hold the values of the "enumeration" and make each value of type 
    /// <see cref="Enum{T}"/> (where T is your new class)</para>
    /// <para>Where you want to save the value for serialization/deserialization, make the value of type
    /// <see cref="Enum{T}"/> where T is your new class</para>
    /// </summary>
    /// <example>
    /// This example shows how to create an Enum with forwards compatiblity and to use it in a serialized object.
    /// <code>
    /// public sealed class FoodTypes : EnumType
    /// {
    ///     public static readonly Enum&lt;FootTypes&gt; Unknown = new Enum&lt;FootTypes&gt;("Unknown");
    ///     public static readonly Enum&lt;FootTypes&gt; Pizza = new Enum&lt;FootTypes&gt;("Pizza");
    ///     public static readonly Enum&lt;FootTypes&gt; Pie = new Enum&lt;FootTypes&gt;("Pie");
    ///     public static readonly Enum&lt;FootTypes&gt; Soup = new Enum&lt;FootTypes&gt;("Soup");
    ///     
    ///     static FoodTypes() { Enum&lt;FootTypes&gt;.SetDefault("Unknown"); }
    /// }
    /// 
    /// [XmlRoot("Food")]
    /// public class MySerializedFood
    /// {
    ///     public string Name;
    ///     public Enum&lt;FootTypes&gt; FoodType;
    /// }
    /// </code>
    /// 
    /// When the above serialized class is serialized, it produces the following XML:
    /// <code>
    /// &lt;Food&gt;
    ///    &lt;Name&gt;Meatlovers Pizza&lt;/Name&gt;
    ///    &lt;FoodType&gt;Pizza&lt;/FoodType&gt;
    /// &lt;/Food&gt;
    /// </code>
    /// </example>
    /// <remarks>Marked as <see cref="StableAPI"/></remarks>
    [Serializable]
    public struct Enum<T> : IComparable<Enum<T>> where T : class, EnumType
    {
        /// <summary>
        /// Represents a null value. Equivalent to <c>new Enum&lt;T&gt;(null)</c>
        /// </summary>
        /// <remarks>Marked as <see cref="StableAPI"/></remarks>
        public static readonly Enum<T> Null = new Enum<T>(null);

        private string internalValue;

        /// <summary>
        /// Creates a new StringEnum with the specified value
        /// </summary>
        /// <param name="value">The internal value that will be serialized/deserialized (can be null)</param>
        /// <remarks>Marked as <see cref="StableAPI"/></remarks>
        public Enum(string value)
        {
            internalValue = value;
        }

        /// <summary>
        /// Gets or sets the internal value for this enum. Use with care.
        /// SHOULD ONLY BE USED FOR SERIALIZATION / DESERIALIZATION
        /// </summary>
        /// <remarks>Marked as <see cref="StableAPI"/></remarks>
        [XmlText]
        public string InternalValue
        {
            get { return internalValue ?? ""; }
            set { internalValue = value; }
        }

        #region Overridden methods
        public override int GetHashCode()
        {
            return InternalValue != null ? InternalValue.GetHashCode() : 0;
        }

        public override bool Equals(object obj)
        {
            return obj is Enum<T> && ((Enum<T>)obj).InternalValue == InternalValue;
        }

        public int CompareTo(Enum<T> other)
        {
            return string.Compare(InternalValue, other.InternalValue, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return InternalValue;
        }
        #endregion

        #region Operator overrides
        public static bool operator ==(Enum<T> se1, Enum<T> se2)
        {
            return se1.InternalValue == se2.InternalValue;
        }

        public static bool operator !=(Enum<T> se1, Enum<T> se2)
        {
            return !(se1 == se2);
        }
        #endregion
    }

    /// <summary>
    /// Interface for restricting the types <see cref="Enum{T}"/> can work with
    /// </summary>
    /// <remarks>Marked as <see cref="StableAPI"/></remarks>
    public interface EnumType
    {
    }
}
