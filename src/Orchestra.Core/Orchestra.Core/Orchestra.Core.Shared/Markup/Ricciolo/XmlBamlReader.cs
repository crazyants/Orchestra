#if NET

#pragma warning disable 1591 // 1591 = missing xml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Windows.Media;

namespace Orchestra.StylesExplorer.MarkupReflection
{
    internal class XmlBamlReader : XmlReader, IXmlNamespaceResolver
    {

        #region Fields

        private BamlBinaryReader reader;
        private Hashtable assemblyTable = new Hashtable();
        private Hashtable stringTable = new Hashtable();
        private Hashtable typeTable = new Hashtable();
        private Hashtable propertyTable = new Hashtable();

        private readonly ITypeResolver _resolver;

        private BamlRecordType currentType;

        private Stack<XmlBamlElement> elements = new Stack<XmlBamlElement>();
        private Stack<XmlBamlElement> readingElements = new Stack<XmlBamlElement>();
        private Stack<KeysResourcesCollection> keysResources = new Stack<KeysResourcesCollection>();
        private NodesCollection nodes = new NodesCollection();
        private List<XmlPIMapping> _mappings = new List<XmlPIMapping>();
        private XmlBamlNode _currentNode;

        private readonly KnownInfo KnownInfo;

        private int complexPropertyOpened = 0;

        private bool intoAttribute = false;
        private bool initialized;
        private bool _eof;

        private bool isPartialDefKeysClosed = true;
        private bool isDefKeysClosed = true;

        private int bytesToSkip;

        private static readonly MethodInfo staticConvertCustomBinaryToObjectMethod = Type.GetType("System.Windows.Markup.XamlPathDataSerializer,PresentationFramework, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35").GetMethod("StaticConvertCustomBinaryToObject", BindingFlags.Static | BindingFlags.Public);
        private readonly TypeDeclaration XamlTypeDeclaration;
        private readonly XmlNameTable _nameTable = new NameTable();
        private IDictionary<string, string> _rootNamespaces;

        #endregion

        public XmlBamlReader(Stream stream) : this (stream, AppDomainTypeResolver.GetIntoNewAppDomain(Environment.CurrentDirectory))
        {
            
        }

        public XmlBamlReader(Stream stream, ITypeResolver resolver)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            _resolver = resolver;
            reader = new BamlBinaryReader(stream);

            XamlTypeDeclaration = new TypeDeclaration(this.Resolver, "", "System.Windows.Markup", 0);
            KnownInfo = new KnownInfo(resolver);
        }

        ///<summary>
        ///When overridden in a derived class, gets the value of the attribute with the specified <see cref="P:System.Xml.XmlReader.Name"></see>.
        ///</summary>
        ///
        ///<returns>
        ///The value of the specified attribute. If the attribute is not found, null is returned.
        ///</returns>
        ///
        ///<param name="name">The qualified name of the attribute. </param>
        public override string GetAttribute(string name)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, gets the value of the attribute with the specified <see cref="P:System.Xml.XmlReader.LocalName"></see> and <see cref="P:System.Xml.XmlReader.NamespaceURI"></see>.
        ///</summary>
        ///
        ///<returns>
        ///The value of the specified attribute. If the attribute is not found, null is returned. This method does not move the reader.
        ///</returns>
        ///
        ///<param name="namespaceURI">The namespace URI of the attribute. </param>
        ///<param name="name">The local name of the attribute. </param>
        public override string GetAttribute(string name, string namespaceURI)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, gets the value of the attribute with the specified index.
        ///</summary>
        ///
        ///<returns>
        ///The value of the specified attribute. This method does not move the reader.
        ///</returns>
        ///
        ///<param name="i">The index of the attribute. The index is zero-based. (The first attribute has index 0.) </param>
        public override string GetAttribute(int i)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, moves to the attribute with the specified <see cref="P:System.Xml.XmlReader.Name"></see>.
        ///</summary>
        ///
        ///<returns>
        ///true if the attribute is found; otherwise, false. If false, the reader's position does not change.
        ///</returns>
        ///
        ///<param name="name">The qualified name of the attribute. </param>
        public override bool MoveToAttribute(string name)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, moves to the attribute with the specified <see cref="P:System.Xml.XmlReader.LocalName"></see> and <see cref="P:System.Xml.XmlReader.NamespaceURI"></see>.
        ///</summary>
        ///
        ///<returns>
        ///true if the attribute is found; otherwise, false. If false, the reader's position does not change.
        ///</returns>
        ///
        ///<param name="name">The local name of the attribute. </param>
        ///<param name="ns">The namespace URI of the attribute. </param>
        public override bool MoveToAttribute(string name, string ns)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, moves to the first attribute.
        ///</summary>
        ///
        ///<returns>
        ///true if an attribute exists (the reader moves to the first attribute); otherwise, false (the position of the reader does not change).
        ///</returns>
        ///
        public override bool MoveToFirstAttribute()
        {
            intoAttribute = false;
            if (nodes.Count > 0 && nodes.Peek() is XmlBamlProperty)
            {
                _currentNode = nodes.Dequeue();
                return true;
            }
            return false;
        }

        ///<summary>
        ///When overridden in a derived class, moves to the next attribute.
        ///</summary>
        ///
        ///<returns>
        ///true if there is a next attribute; false if there are no more attributes.
        ///</returns>
        ///
        public override bool MoveToNextAttribute()
        {
            intoAttribute = false;
            if (nodes.Count > 0 && nodes.Peek() is XmlBamlProperty)
            {
                _currentNode = nodes.Dequeue();
                return true;
            }
            return false;
        }

        ///<summary>
        ///When overridden in a derived class, moves to the element that contains the current attribute node.
        ///</summary>
        ///
        ///<returns>
        ///true if the reader is positioned on an attribute (the reader moves to the element that owns the attribute); false if the reader is not positioned on an attribute (the position of the reader does not change).
        ///</returns>
        ///
        public override bool MoveToElement()
        {
            while (nodes.Peek() is XmlBamlProperty)
            {
                nodes.Dequeue();
            }

            return true;
        }

        ///<summary>
        ///When overridden in a derived class, parses the attribute value into one or more Text, EntityReference, or EndEntity nodes.
        ///</summary>
        ///
        ///<returns>
        ///true if there are nodes to return.false if the reader is not positioned on an attribute node when the initial call is made or if all the attribute values have been read.An empty attribute, such as, misc="", returns true with a single node with a value of String.Empty.
        ///</returns>
        ///
        public override bool ReadAttributeValue()
        {
            if (!intoAttribute)
            {
                intoAttribute = true;
                return true;
            }
            return false;
        }

        ///<summary>
        ///When overridden in a derived class, reads the next node from the stream.
        ///</summary>
        ///
        ///<returns>
        ///true if the next node was read successfully; false if there are no more nodes to read.
        ///</returns>
        ///
        ///<exception cref="T:System.Xml.XmlException">An error occurred while parsing the XML. </exception>
        public override bool Read()
        {
            return ReadInternal();
        }

        private bool ReadInternal()
        {
            EnsureInit();

            if (SetNextNode())
                return true;

            try
            {
                do
                {
                    currentType = (BamlRecordType)reader.ReadByte();
                    //Debug.WriteLine(currentType);
                    if (currentType == BamlRecordType.DocumentEnd) break;

                    long position = reader.BaseStream.Position;

                    ComputeBytesToSkip();
                    ProcessNext();

                    if (bytesToSkip > 0)
                    {
                        reader.BaseStream.Position = position + bytesToSkip;
                    }
                }
                //while (currentType != BamlRecordType.DocumentEnd);
                while (nodes.Count == 0 || (currentType != BamlRecordType.ElementEnd) || complexPropertyOpened > 0);

                return SetNextNode();
            }
            catch (EndOfStreamException)
            {
                _eof = true;
                return false;
            }
        }

        private bool SetNextNode()
        {
            while (nodes.Count > 0)
            {
                _currentNode = nodes.Dequeue();

                if ((_currentNode is XmlBamlProperty)) continue;

                if (this.NodeType == XmlNodeType.EndElement)
                {
                    if (readingElements.Count == 1)
                        _rootNamespaces = ((IXmlNamespaceResolver)this).GetNamespacesInScope(XmlNamespaceScope.All);
                    readingElements.Pop();
                }
                else if (this.NodeType == XmlNodeType.Element)
                    readingElements.Push((XmlBamlElement)_currentNode);

                return true;
            }

            return false;
        }

        private void ProcessNext()
        {
            switch (currentType)
            {
                case BamlRecordType.DocumentStart:
                    {
                        reader.ReadBoolean();
                        reader.ReadInt32();
                        reader.ReadBoolean();
                        break;
                    }
                case BamlRecordType.DocumentEnd:
                    break;
                case BamlRecordType.ElementStart:
                    this.ReadElementStart();
                    break;
                case BamlRecordType.ElementEnd:
                    this.ReadElementEnd();
                    break;
                case BamlRecordType.AssemblyInfo:
                    this.ReadAssemblyInfo();
                    break;
                case BamlRecordType.StringInfo:
                    this.ReadStringInfo();
                    break;
                case BamlRecordType.LineNumberAndPosition:
                    reader.ReadInt32();
                    reader.ReadInt32();
                    break;
                case BamlRecordType.LinePosition:
                    reader.ReadInt32();
                    break;
                case BamlRecordType.XmlnsProperty:
                    this.ReadXmlnsProperty();
                    break;
                case BamlRecordType.ConnectionId:
                    reader.ReadInt32();
                    break;
                case BamlRecordType.DeferableContentStart:
                    reader.ReadInt32();
                    break;
                case BamlRecordType.DefAttribute:
                    this.ReadDefAttribute();
                    break;
                case BamlRecordType.DefAttributeKeyType:
                    this.ReadDefAttributeKeyType();
                    break;
                case BamlRecordType.DefAttributeKeyString:
                    this.ReadDefAttributeKeyString();
                    break;
                case BamlRecordType.AttributeInfo:
                    this.ReadAttributeInfo();
                    break;
                case BamlRecordType.PropertyListStart:
                    this.ReadPropertyListStart();
                    break;
                case BamlRecordType.PropertyListEnd:
                    this.ReadPropertyListEnd();
                    break;
                case BamlRecordType.Property:
                    this.ReadProperty();
                    break;
                case BamlRecordType.PropertyWithConverter:
                    this.ReadPropertyWithConverter();
                    break;
                case BamlRecordType.PropertyWithExtension:
                    this.ReadPropertyWithExtension();
                    break;
                case BamlRecordType.PropertyDictionaryStart:
                    this.ReadPropertyDictionaryStart();
                    break;
                case BamlRecordType.PropertyCustom:
                    this.ReadPropertyCustom();
                    break;
                case BamlRecordType.PropertyDictionaryEnd:
                    this.ReadPropertyDictionaryEnd();
                    break;
                case BamlRecordType.PropertyComplexStart:
                    this.ReadPropertyComplexStart();
                    break;
                case BamlRecordType.PropertyComplexEnd:
                    this.ReadPropertyComplexEnd();
                    break;
                case BamlRecordType.PIMapping:
                    this.ReadPIMapping();
                    break;
                case BamlRecordType.TypeInfo:
                    this.ReadTypeInfo();
                    break;
                case BamlRecordType.ContentProperty:
                    this.ReadContentProperty();
                    break;
                case BamlRecordType.ConstructorParametersStart:
                    ReadConstructorParametersStart();
                    break;
                case BamlRecordType.ConstructorParametersEnd:
                    ReadConstructorParametersEnd();
                    break;
                case BamlRecordType.ConstructorParameterType:
                    this.ReadConstructorParameterType();
                    break;
                case BamlRecordType.Text:
                    this.ReadText();
                    break;
                case BamlRecordType.TextWithConverter:
                    this.ReadTextWithConverter();
                    break;
                case BamlRecordType.PropertyWithStaticResourceId:
                    this.ReadPropertyWithStaticResourceIdentifier();
                    break;
                case BamlRecordType.OptimizedStaticResource:
                    this.ReadOptimizedStaticResource();
                    break;
                case BamlRecordType.KeyElementStart:
                    this.ReadKeyElementStart();
                    break;
                case BamlRecordType.KeyElementEnd:
                    this.ReadKeyElementEnd();
                    break;
                case BamlRecordType.PropertyTypeReference:
                    this.ReadPropertyTypeReference();
                    break;
                case BamlRecordType.StaticResourceStart:
                    ReadStaticResourceStart();
                    break;
                case BamlRecordType.StaticResourceEnd:
                    ReadStaticResourceEnd();
                    break;
                case BamlRecordType.StaticResourceId:
                    ReadStaticResourceId();
                    break;
                case BamlRecordType.PresentationOptionsAttribute:
                    this.ReadPresentationOptionsAttribute();
                    break;
                default:
                    break;
            }
        }

        private void ComputeBytesToSkip()
        {
            bytesToSkip = 0;
            switch (currentType)
            {
                case BamlRecordType.PropertyWithConverter:
                case BamlRecordType.DefAttributeKeyString:
                case BamlRecordType.PresentationOptionsAttribute:
                case BamlRecordType.Property:
                case BamlRecordType.PropertyCustom:
                case BamlRecordType.Text:
                case BamlRecordType.TextWithConverter:
                case BamlRecordType.XmlnsProperty:
                case BamlRecordType.DefAttribute:
                case BamlRecordType.PIMapping:
                case BamlRecordType.AssemblyInfo:
                case BamlRecordType.TypeInfo:
                case BamlRecordType.AttributeInfo:
                case BamlRecordType.StringInfo:
                    bytesToSkip = reader.ReadCompressedInt32();
                    break;
            }
        }

        private void EnsureInit()
        {
            if (!initialized)
            {
                int startChars = reader.ReadInt32();
                String type = new String(new BinaryReader(this.reader.BaseStream, Encoding.Unicode).ReadChars(startChars >> 1));
                if (type != "MSBAML")
                    throw new NotSupportedException("Not a MS BAML");

                int r = reader.ReadInt32();
                int s = reader.ReadInt32();
                int t = reader.ReadInt32();
                if (((r != 0x600000) || (s != 0x600000)) || (t != 0x600000))
                    throw new NotSupportedException();

                initialized = true;
            }
        }

        ///<summary>
        ///When overridden in a derived class, changes the <see cref="P:System.Xml.XmlReader.ReadState"></see> to Closed.
        ///</summary>
        ///
        public override void Close()
        {
            //if (reader != null)
            //    reader.Close();
            reader = null;
        }

        ///<summary>
        ///When overridden in a derived class, resolves a namespace prefix in the current element's scope.
        ///</summary>
        ///
        ///<returns>
        ///The namespace URI to which the prefix maps or null if no matching prefix is found.
        ///</returns>
        ///
        ///<param name="prefix">The prefix whose namespace URI you want to resolve. To match the default namespace, pass an empty string. </param>
        public override string LookupNamespace(string prefix)
        {
            if (readingElements.Count == 0) return null;

            XmlNamespaceCollection namespaces = readingElements.Peek().Namespaces;

            for (int x = 0; x < namespaces.Count; x++)
            {
                if (String.CompareOrdinal(namespaces[x].Prefix, prefix) == 0)
                    return namespaces[x].Namespace;
            }

            return null;
        }

        ///<summary>
        ///When overridden in a derived class, resolves the entity reference for EntityReference nodes.
        ///</summary>
        ///
        ///<exception cref="T:System.InvalidOperationException">The reader is not positioned on an EntityReference node; this implementation of the reader cannot resolve entities (<see cref="P:System.Xml.XmlReader.CanResolveEntity"></see> returns false). </exception>
        public override void ResolveEntity()
        {
            throw new NotImplementedException();
        }

        ///<summary>
        ///When overridden in a derived class, gets the type of the current node.
        ///</summary>
        ///
        ///<returns>
        ///One of the <see cref="T:System.Xml.XmlNodeType"></see> values representing the type of the current node.
        ///</returns>
        ///
        public override XmlNodeType NodeType
        {
            get
            {
                if (intoAttribute) return XmlNodeType.Text;

                return this.CurrentNode.NodeType;
            }
        }

        ///<summary>
        ///When overridden in a derived class, gets the local name of the current node.
        ///</summary>
        ///
        ///<returns>
        ///The name of the current node with the prefix removed. For example, LocalName is book for the element &lt;bk:book&gt;.For node types that do not have a name (like Text, Comment, and so on), this property returns String.Empty.
        ///</returns>
        ///
        public override string LocalName
        {
            get
            {
                if (intoAttribute) return String.Empty;

                String localName = String.Empty;

                XmlBamlNode node = this.CurrentNode;
                if (node is XmlBamlProperty)
                {
                    PropertyDeclaration pd = ((XmlBamlProperty)node).PropertyDeclaration;
                    localName = FormatPropertyDeclaration(pd, false, true, true);
                }
                else if (node is XmlBamlPropertyElement)
                {
                    XmlBamlPropertyElement property = (XmlBamlPropertyElement)node;
                    localName = String.Format("{0}.{1}", property.TypeDeclaration.Name, property.PropertyDeclaration.Name);
                }
                else if (node is XmlBamlElement)
                    localName = ((XmlBamlElement)node).TypeDeclaration.Name;

                localName = this.NameTable.Add(localName);

                return localName;
            }
        }

        private PropertyDeclaration GetPropertyDeclaration(short identifier)
        {
            PropertyDeclaration declaration;
            if (identifier >= 0)
            {
                declaration = (PropertyDeclaration)this.propertyTable[identifier];
            }
            else
            {
                declaration = KnownInfo.KnownPropertyTable[-identifier];
            }
            if (declaration == null)
            {
                throw new NotSupportedException();
            }
            return declaration;
        }

        private object GetResourceName(short identifier)
        {
            if (identifier >= 0)
            {
                PropertyDeclaration declaration = (PropertyDeclaration)this.propertyTable[identifier];
                return declaration;
            }
            else
            {
                identifier = (short)-identifier;
                bool isNotKey = (identifier > 0xe8);
                if (isNotKey)
                    identifier = (short)(identifier - 0xe8);
                ResourceName resource = (ResourceName) KnownInfo.KnownResourceTable[(int)identifier];
                if (!isNotKey)
                    return new ResourceName(resource.Name + "Key");
                return resource;
            }
        }

        private void ReadPropertyDictionaryStart()
        {
            short identifier = reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            XmlBamlElement element = elements.Peek();
            XmlBamlPropertyElement property = new XmlBamlPropertyElement(element, PropertyType.Dictionary, pd);
            elements.Push(property);
            nodes.Enqueue(property);

            isDefKeysClosed = true;
            isPartialDefKeysClosed = true;
        }

        private void ReadPropertyDictionaryEnd()
        {
            keysResources.Pop();
            
            CloseElement();
        }

        private void ReadPropertyCustom()
        {
            short identifier = reader.ReadInt16();
            short serializerTypeId = reader.ReadInt16();
            bool isValueTypeId = (serializerTypeId & 0x4000) == 0x4000;
            if (isValueTypeId)
                serializerTypeId = (short)(serializerTypeId & ~0x4000);

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            string value;
            switch (serializerTypeId)
            {
                case 0x2e8:
                    value = new BrushConverter().ConvertToString(SolidColorBrush.DeserializeFrom(reader));
                    break;
                case 0x2e9:
                    value = new Int32CollectionConverter().ConvertToString(DeserializeInt32CollectionFrom(reader));
                    break;
                case 0x89:

                    short typeIdentifier = reader.ReadInt16();
                    if (isValueTypeId)
                    {
                        TypeDeclaration typeDeclaration = this.GetTypeDeclaration(typeIdentifier);
                        string name = reader.ReadString();
                        value = FormatPropertyDeclaration(new PropertyDeclaration(name, typeDeclaration), true, false, true);
                    }
                    else
                        value = FormatPropertyDeclaration(this.GetPropertyDeclaration(typeIdentifier), true, false, true);
                    break;

                case 0x2ea:
                    value = ((IFormattable)staticConvertCustomBinaryToObjectMethod.Invoke(null, new object[] { this.reader })).ToString("G", CultureInfo.InvariantCulture);
                    break;
                case 0x2eb:
                case 0x2f0:
                    value = Deserialize3DPoints();
                    break;
                case 0x2ec:
                    value = DeserializePoints();
                    break;
                case 0xc3:
                    // Enum
                    uint num = reader.ReadUInt32();
                    value = num.ToString();
                    break;
                case 0x2e:
                    int b = reader.ReadByte();
                    value = (b == 1) ? Boolean.TrueString : Boolean.FalseString;
                    break;
                default:
                    return;
            }

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = value;

            nodes.Enqueue(property);
        }

        private string DeserializePoints()
        {
            using (StringWriter writer = new StringWriter())
            {
                int num10 = reader.ReadInt32();
                for (int k = 0; k < num10; k++)
                {
                    if (k != 0)
                        writer.Write(" ");
                    for (int m = 0; m < 2; m++)
                    {
                        if (m != 0)
                            writer.Write(",");
                        writer.Write(reader.ReadCompressedDouble().ToString());
                    }
                }
                return writer.ToString();
            }
        }

        private String Deserialize3DPoints()
        {
            using (StringWriter writer = new StringWriter())
            {
                int num14 = reader.ReadInt32();
                for (int i = 0; i < num14; i++)
                {
                    if (i != 0)
                    {
                        writer.Write(" ");
                    }
                    for (int j = 0; j < 3; j++)
                    {
                        if (j != 0)
                        {
                            writer.Write(",");
                        }
                        writer.Write(reader.ReadCompressedDouble().ToString());
                    }
                }
                return writer.ToString();
            }
        }

        private static Int32Collection DeserializeInt32CollectionFrom(BinaryReader reader)
        {
            IntegerCollectionType type = (IntegerCollectionType)reader.ReadByte();
            int capacity = reader.ReadInt32();
            if (capacity < 0)
                throw new ArgumentException();

            Int32Collection ints = new Int32Collection(capacity);
            switch (type)
            {
                case IntegerCollectionType.Byte:
                    for (int i = 0; i < capacity; i++)
                    {
                        ints.Add(reader.ReadByte());
                    }
                    return ints;

                case IntegerCollectionType.UShort:
                    for (int j = 0; j < capacity; j++)
                    {
                        ints.Add(reader.ReadUInt16());
                    }
                    return ints;

                case IntegerCollectionType.Integer:
                    for (int k = 0; k < capacity; k++)
                    {
                        int num7 = reader.ReadInt32();
                        ints.Add(num7);
                    }
                    return ints;

                case IntegerCollectionType.Consecutive:
                    for (int m = reader.ReadInt32(); m < capacity; m++)
                    {
                        ints.Add(m);
                    }
                    return ints;
            }
            throw new ArgumentException();
        }

        private void ReadPropertyWithExtension()
        {
            short identifier = reader.ReadInt16();
            short x = reader.ReadInt16();
            short valueIdentifier = reader.ReadInt16();
            bool isValueType = (x & 0x4000) == 0x4000;
            bool isStaticType = (x & 0x2000) == 0x2000;
            x = (short)(x & 0xfff);

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            short extensionIdentifier = (short)-(x & 0xfff);
            string value = String.Empty;

            switch (x)
            {
                case 0x25a:
                    // StaticExtension
                    object resource = this.GetResourceName(valueIdentifier);
                    if (resource is ResourceName)
                        value = this.GetStaticExtension(((ResourceName)resource).Name);
                    else if (resource is PropertyDeclaration)
                        value = this.GetStaticExtension(FormatPropertyDeclaration(((PropertyDeclaration)resource), true, false, false));
                    break;
                case 0x25b: // StaticResource
                case 0xbd: // DynamicResource
                    if (isValueType)
                    {
                        value = this.GetTypeExtension(valueIdentifier);
                    }
                    else if (isStaticType)
                    {
                        TypeDeclaration extensionDeclaration = this.GetTypeDeclaration(extensionIdentifier);
                        value = GetExtension(extensionDeclaration, GetStaticExtension(GetResourceName(valueIdentifier).ToString()));
                    }
                    else
                    {
                        TypeDeclaration extensionDeclaration = this.GetTypeDeclaration(extensionIdentifier);
                        value = GetExtension(extensionDeclaration, (string)this.stringTable[valueIdentifier]);
                    }
                    break;

                case 0x27a:
                    // TemplateBinding
                    PropertyDeclaration pdValue = this.GetPropertyDeclaration(valueIdentifier);
                    value = GetTemplateBindingExtension(pdValue);
                    break;
                default:
                    throw new NotSupportedException("Unknown property with extension");
            }

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = value;

            nodes.Enqueue(property);
        }

        private void ReadProperty()
        {
            short identifier = reader.ReadInt16();
            string text = reader.ReadString();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = text;

            nodes.Enqueue(property);
        }

        private void ReadPropertyWithConverter()
        {
            short identifier = reader.ReadInt16();
            string text = reader.ReadString();
            reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = text;

            nodes.Enqueue(property);
        }

        private void ReadAttributeInfo()
        {
            short key = reader.ReadInt16();
            short identifier = reader.ReadInt16();
            reader.ReadByte();
            string name = reader.ReadString();
            TypeDeclaration declaringType = this.GetTypeDeclaration(identifier);
            PropertyDeclaration declaration2 = new PropertyDeclaration(name, declaringType);
            this.propertyTable.Add(key, declaration2);
        }

        private void ReadDefAttributeKeyType()
        {
            short typeIdentifier = reader.ReadInt16();
            reader.ReadByte();
            int position = reader.ReadInt32();
            // TODO: shared
            bool shared = reader.ReadBoolean();
            bool sharedSet = reader.ReadBoolean();

            // TODO: handle shared
            AddDefKey(position, this.GetTypeExtension(typeIdentifier));
        }

        private void ReadDefAttribute()
        {
            string text = reader.ReadString();
            short identifier = reader.ReadInt16();

            PropertyDeclaration pd;
            switch (identifier)
            {
                case -2:
                    pd = new PropertyDeclaration("Uid", XamlTypeDeclaration);
                    break;
                case -1:
                    pd = new PropertyDeclaration("Name", XamlTypeDeclaration);
                    break;
                default:
                    string recordName = (string)this.stringTable[identifier];
                    if (recordName != "Key") throw new NotSupportedException(recordName);
                    pd = new PropertyDeclaration(recordName, XamlTypeDeclaration);

                    AddDefKey(-1, text);
                    break;
            }

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Key, pd);
            property.Value = text;

            nodes.Enqueue(property);
        }

        private void ReadDefAttributeKeyString()
        {
            short num = reader.ReadInt16();
            int position = reader.ReadInt32();
            //bool shared = reader.ReadBoolean();
            //bool sharedSet = reader.ReadBoolean();
            string text = (string)this.stringTable[num];
            if (text == null)
                throw new NotSupportedException();

            AddDefKey(position, text);
        }

        private void AddDefKey(long position, string text)
        {
            // Guardo se la dichiarazione delle chiavi risulta chiusa
            // Se � aperta c'� un sotto ResourceDictionary oppure � il root ResourceDictionary
            if (isDefKeysClosed)
            {
                keysResources.Push(new KeysResourcesCollection());
            }

            // Guardo se � stata chiusa la dichiarazione parziale (mediante dichiarazione OptimizedStaticResource)
            // Si chiude il ciclo di chiavi
            if (isPartialDefKeysClosed)
            {
                keysResources.Peek().Add(new KeysResource());
            }
            isDefKeysClosed = false;
            isPartialDefKeysClosed = false;

            // TODO: handle shared
            if (position >= 0)
                keysResources.Peek().Last.Keys[position] = text;
        }

        private void ReadXmlnsProperty()
        {
            string prefix = reader.ReadString();
            string @namespace = reader.ReadString();
            string[] textArray = new string[(uint)reader.ReadInt16()];
            for (int i = 0; i < textArray.Length; i++)
            {
                textArray[i] = (string)this.assemblyTable[reader.ReadInt16()];
            }

            XmlNamespaceCollection namespaces = elements.Peek().Namespaces;
            // Mapping locale, ci aggiunto l'assembly
            if (@namespace.StartsWith("clr-namespace:") && @namespace.IndexOf("assembly=") < 0)
            {
                XmlPIMapping mappingToChange = null;
                foreach (XmlPIMapping mapping in this.Mappings)
                {
                    if (String.CompareOrdinal(mapping.XmlNamespace, @namespace) == 0)
                    {
                        mappingToChange = mapping;
                        break;
                    }
                }
                if (mappingToChange == null)
                    throw new InvalidOperationException("Cannot find mapping");

                @namespace = String.Format("{0};assembly={1}", @namespace, GetAssembly(mappingToChange.AssemblyId).Replace(" ", ""));
                mappingToChange.XmlNamespace = @namespace;
            }
            namespaces.Add(new XmlNamespace(prefix, @namespace));
        }

        private void ReadElementEnd()
        {
            CloseElement();

            // Provvedo all'eliminazione del gruppo di chiavi se sono sul root ResourceDictionary
            // e si � chiuso uno degli elementi di primo livello e tutte le chiavi sono state usate
            // Passo alla prossima lista
            KeysResource keysResource = (elements.Count == 1 && keysResources.Count > 0) ? keysResources.Peek().First : null;
            if (keysResource != null && keysResource.Keys.Count == 0)
                keysResources.Peek().RemoveAt(0);
        }

        private void ReadPropertyComplexStart()
        {
            short identifier = reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            XmlBamlElement element = FindXmlBamlElement();

            XmlBamlPropertyElement property = new XmlBamlPropertyElement(element, PropertyType.Complex, pd);
            elements.Push(property);
            nodes.Enqueue(property);
            complexPropertyOpened++;
        }

        private XmlBamlElement FindXmlBamlElement()
        {
            return elements.Peek();

            //XmlBamlElement element;
            //int x = nodes.Count - 1;
            //do
            //{
            //    element = nodes[x] as XmlBamlElement;
            //    x--;
            //} while (element == null);
            //return element;
        }

        private void ReadPropertyListStart()
        {
            short identifier = reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            XmlBamlElement element = FindXmlBamlElement();
            XmlBamlPropertyElement property = new XmlBamlPropertyElement(element, PropertyType.List, pd);
            elements.Push(property);
            nodes.Enqueue(property);
        }

        private void ReadPropertyListEnd()
        {
            CloseElement();
        }

        private void ReadPropertyComplexEnd()
        {
            XmlBamlPropertyElement propertyElement = (XmlBamlPropertyElement) elements.Peek();

            CloseElement();

            complexPropertyOpened--;
            // Valuto se contiene tutte extension
            int start = nodes.IndexOf(propertyElement) + 1;
            IEnumerator enumerator = nodes.GetEnumerator();

            int c = 0;
            while (c < start && enumerator.MoveNext())
                c++;

            if (IsExtension(enumerator))
            {
                start--;
                nodes.RemoveAt(start);
                nodes.RemoveLast();

                StringBuilder sb = new StringBuilder();
                FormatElementExtension((XmlBamlElement) nodes[start], sb);

                XmlBamlProperty property =
                    new XmlBamlProperty(PropertyType.Complex, propertyElement.PropertyDeclaration);
                property.Value = sb.ToString();
                nodes.Add(property);

                return;
            }
        }

        private void FormatElementExtension(XmlBamlElement element, StringBuilder sb)
        {
            sb.Append("{");
            sb.Append(FormatTypeDeclaration(element.TypeDeclaration));

            int start = nodes.IndexOf(element);
            nodes.RemoveAt(start);

            string sep = " ";
            while (nodes.Count > start)
            {
                XmlBamlNode node = nodes[start];

                if (node is XmlBamlEndElement)
                {
                    sb.Append("}");
                    nodes.RemoveAt(start);
                    break;
                }
                else if (node is XmlBamlPropertyElement)
                {
                    nodes.RemoveAt(start);

                    sb.Append(sep);
                    XmlBamlPropertyElement property = (XmlBamlPropertyElement)node;
                    sb.Append(property.PropertyDeclaration.Name);
                    sb.Append("=");

                    node = nodes[start];
                    nodes.RemoveLast();
                    FormatElementExtension((XmlBamlElement)node, sb);
                }
                else if (node is XmlBamlElement)
                {
                    sb.Append(sep);
                    FormatElementExtension((XmlBamlElement)node, sb);
                }
                else if (node is XmlBamlProperty)
                {
                    nodes.RemoveAt(start);

                    sb.Append(sep);
                    XmlBamlProperty property = (XmlBamlProperty)node;
                    sb.Append(property.PropertyDeclaration.Name);
                    sb.Append("=");
                    sb.Append(property.Value);
                }
                else if (node is XmlBamlText)
                {
                    nodes.RemoveAt(start);

                    sb.Append(sep);
                    sb.Append(((XmlBamlText)node).Text);
                }
                sep = ",";
            }
        }

        private static bool IsExtension(IEnumerator enumerator)
        {
            bool r = true;
            while (enumerator.MoveNext() && r)
            {
                object node = enumerator.Current;
                if (node is XmlBamlElement && !(node is XmlBamlEndElement) && !((XmlBamlElement)node).TypeDeclaration.IsExtension)
                {
                    r = false;
                }
            }

            return r;
        }

        private void CloseElement()
        {
            nodes.Enqueue(new XmlBamlEndElement(elements.Pop()));
        }

        private void ReadElementStart()
        {
            short identifier = reader.ReadInt16();
            reader.ReadByte();
            TypeDeclaration declaration = GetTypeDeclaration(identifier);

            XmlBamlElement element;
            XmlBamlElement parentElement = null;
            if (elements.Count > 0)
            {
                parentElement = elements.Peek();
                element = new XmlBamlElement(parentElement);
                element.Position = this.reader.BaseStream.Position;

                // Porto l'inizio del padre all'inizio del primo figlio
                if (parentElement.Position == 0 && complexPropertyOpened == 0)
                    parentElement.Position = element.Position;
            }
            else
                element = new XmlBamlElement();

            element.TypeDeclaration = declaration;
            elements.Push(element);
            nodes.Enqueue(element);

            if (parentElement != null && complexPropertyOpened == 0)
            {
                // Calcolo la posizione dell'elemento rispetto al padre
                long position = element.Position - parentElement.Position;
                KeysResource keysResource = (keysResources.Count > 0) ? keysResources.Peek().First : null;
                if (keysResource != null && keysResource.Keys.HasKey(position))
                {
                    string key = keysResource.Keys[position];
                    // Rimuovo la chiave perch� � stata usata
                    keysResource.Keys.Remove(position);

                    AddKeyToElement(key);
                }
            }
        }

        private void AddKeyToElement(string key)
        {
            PropertyDeclaration pd = new PropertyDeclaration("Key", XamlTypeDeclaration);
            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Key, pd);

            property.Value = key;

            nodes.Enqueue(property);
        }

        private XmlPIMapping FindByClrNamespaceAndAssemblyId(TypeDeclaration declaration)
        {
            return FindByClrNamespaceAndAssemblyId(declaration.Namespace, declaration.AssemblyId);
        }

        private XmlPIMapping FindByClrNamespaceAndAssemblyId(string clrNamespace, int assemblyId)
        {
            if (clrNamespace == XamlTypeDeclaration.Namespace && assemblyId == XamlTypeDeclaration.AssemblyId)
                return new XmlPIMapping(XmlPIMapping.XamlNamespace, 0, clrNamespace);

            for (int x = 0; x < Mappings.Count; x++)
            {
                XmlPIMapping xp = Mappings[x];
                if (xp.AssemblyId == assemblyId && String.CompareOrdinal(xp.ClrNamespace, clrNamespace) == 0)
                    return xp;
            }

            return null;
        }

        private void ReadPIMapping()
        {
            string xmlNamespace = reader.ReadString();
            string clrNamespace = reader.ReadString();
            short assemblyId = reader.ReadInt16();

            Mappings.Add(new XmlPIMapping(xmlNamespace, assemblyId, clrNamespace));
        }

        private void ReadContentProperty()
        {
            reader.ReadInt16();

            // Non serve aprire niente, � il default
        }

        private static void ReadConstructorParametersStart()
        {
            //this.constructorParameterTable.Add(this.elements.Peek());
            //PromoteDataToComplexProperty();
        }

        private static void ReadConstructorParametersEnd()
        {
            //this.constructorParameterTable.Remove(this.elements.Peek());
            //properties.Pop();
        }

        private void ReadConstructorParameterType()
        {
            short identifier = reader.ReadInt16();

            //TypeDeclaration declaration = GetTypeDeclaration(identifier);
            nodes.Enqueue(new XmlBamlText(GetTypeExtension(identifier)));
        }

        private void ReadText()
        {
            string text = reader.ReadString();

            nodes.Enqueue(new XmlBamlText(text));
        }

        private void ReadKeyElementStart()
        {
            short typeIdentifier = reader.ReadInt16();
            byte valueIdentifier = reader.ReadByte();
            // TODO: handle shared
            //bool shared = (valueIdentifier & 1) != 0;
            //bool sharedSet = (valueIdentifier & 2) != 0;
            int position = reader.ReadInt32();
            reader.ReadBoolean();
            reader.ReadBoolean();

            TypeDeclaration declaration = this.GetTypeDeclaration(typeIdentifier);

            XmlBamlPropertyElement property = new XmlBamlPropertyElement(elements.Peek(), PropertyType.Key, new PropertyDeclaration("Key", declaration));
            property.Position = position;
            elements.Push(property);
            nodes.Enqueue(property);
            complexPropertyOpened++;
        }

        private void ReadKeyElementEnd()
        {
            XmlBamlPropertyElement propertyElement = (XmlBamlPropertyElement)elements.Peek();

            CloseElement();
            complexPropertyOpened--;
            if (complexPropertyOpened == 0)
            {

                int start = nodes.IndexOf(propertyElement);

                StringBuilder sb = new StringBuilder();
                FormatElementExtension((XmlBamlElement)nodes[start], sb);
                AddDefKey(propertyElement.Position, sb.ToString());
            }
        }

        private static void ReadStaticResourceStart()
        {
            //short identifier = reader.ReadInt16();
            //byte n = reader.ReadByte();
            //TypeDeclaration declaration = this.GetTypeDeclaration(identifier);
            //this.staticResourceTable.Add(declaration);

            throw new NotImplementedException("StaticResourceStart");
        }

        private static void ReadStaticResourceEnd()
        {
            throw new NotImplementedException("ReadStaticResourceEnd");
        }

        private static void ReadStaticResourceId()
        {
            //short identifier = reader.ReadInt16();
            //object staticResource = this.GetStaticResource(identifier);
            //TypeDeclaration declaration = this.GetTypeDeclaration(-603);

            throw new NotImplementedException("StaticResourceId");
        }

        private void ReadPresentationOptionsAttribute()
        {
            string text = reader.ReadString();
            short valueIdentifier = reader.ReadInt16();

            PropertyDeclaration pd = new PropertyDeclaration(this.stringTable[valueIdentifier].ToString());

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = text;
        }

        private void ReadPropertyTypeReference()
        {
            short identifier = reader.ReadInt16();
            short typeIdentifier = reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            string value = this.GetTypeExtension(typeIdentifier);

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = value;

            nodes.Enqueue(property);
        }

        private void ReadOptimizedStaticResource()
        {
            byte num = reader.ReadByte();
            short typeIdentifier = reader.ReadInt16();
            bool isValueType = (num & 1) == 1;
            bool isStaticType = (num & 2) == 2;
            object resource;

            if (isValueType)
                resource = this.GetTypeExtension(typeIdentifier);
            else if (isStaticType)
            {
                ResourceName resourceName = (ResourceName)this.GetResourceName(typeIdentifier);
                resource = GetStaticExtension(resourceName.Name);
            }
            else
            {
                resource = this.stringTable[typeIdentifier];
            }

            //this.staticResourceTable.Add(resource);
            isPartialDefKeysClosed = true;
            // Aggiungo la risorsa nell'ultimo gruppo
            keysResources.Peek().Last.StaticResources.Add(resource);
        }

        private string GetTemplateBindingExtension(PropertyDeclaration propertyDeclaration)
        {
            return String.Format("{{TemplateBinding {0}}}", FormatPropertyDeclaration(propertyDeclaration, true, false, false));
        }

        private string GetStaticExtension(string name)
        {
            string prefix = this.LookupPrefix(XmlPIMapping.XamlNamespace, false);
            if (String.IsNullOrEmpty(prefix))
                return String.Format("{{Static {0}}}", name);
            else
                return String.Format("{{{0}:Static {1}}}", prefix, name);
        }

        private string GetExtension(TypeDeclaration declaration, string value)
        {
            return String.Format("{{{0} {1}}}", FormatTypeDeclaration(declaration), value);
        }

        private string GetTypeExtension(short typeIdentifier)
        {
            string prefix = this.LookupPrefix(XmlPIMapping.XamlNamespace, false);
            if (String.IsNullOrEmpty(prefix))
                return String.Format("{{Type {0}}}", FormatTypeDeclaration(GetTypeDeclaration(typeIdentifier)));
            else
                return String.Format("{{{0}:Type {1}}}", prefix, FormatTypeDeclaration(GetTypeDeclaration(typeIdentifier)));
        }

        private string FormatTypeDeclaration(TypeDeclaration typeDeclaration)
        {
            XmlPIMapping mapping = FindByClrNamespaceAndAssemblyId(typeDeclaration.Namespace, typeDeclaration.AssemblyId);
            string prefix = (mapping != null) ? this.LookupPrefix(mapping.XmlNamespace, false) : null;
            string name = typeDeclaration.Name;
            if (name.EndsWith("Extension"))
                name = name.Substring(0, name.Length - 9);
            if (String.IsNullOrEmpty(prefix))
                return name;
            else
                return String.Format("{0}:{1}", prefix, name);
        }



        private string FormatPropertyDeclaration(PropertyDeclaration propertyDeclaration, bool withPrefix, bool useReading, bool checkType)
        {
            StringBuilder sb = new StringBuilder();

            TypeDeclaration elementDeclaration = (useReading) ? readingElements.Peek().TypeDeclaration : elements.Peek().TypeDeclaration;

            IDependencyPropertyDescriptor descriptor = null;
            bool areValidTypes = elementDeclaration.Type != null && propertyDeclaration.DeclaringType.Type != null;
            if (areValidTypes)
                descriptor = this.Resolver.GetDependencyPropertyDescriptor(propertyDeclaration.Name, elementDeclaration.Type, propertyDeclaration.DeclaringType.Type);

            bool isDescendant = (areValidTypes && (propertyDeclaration.DeclaringType.Type.Equals(elementDeclaration.Type) || elementDeclaration.Type.IsSubclassOf(propertyDeclaration.DeclaringType.Type)));
            bool isAttached = (descriptor != null && descriptor.IsAttached);
            bool differentType = ((propertyDeclaration.DeclaringType != propertyDeclaration.DeclaringType || !isDescendant));

            if (withPrefix)
            {
                XmlPIMapping mapping = FindByClrNamespaceAndAssemblyId(propertyDeclaration.DeclaringType.Namespace, propertyDeclaration.DeclaringType.AssemblyId);
                string prefix = (mapping != null) ? this.LookupPrefix(mapping.XmlNamespace, false) : null;

                if (!String.IsNullOrEmpty(prefix))
                {
                    sb.Append(prefix);
                    sb.Append(":");
                }
            }
            if ((differentType || isAttached || !checkType) && propertyDeclaration.DeclaringType.Name.Length > 0)
            {
                sb.Append(propertyDeclaration.DeclaringType.Name);
                sb.Append(".");
            }
            sb.Append(propertyDeclaration.Name);

            return sb.ToString();
        }

        private void ReadPropertyWithStaticResourceIdentifier()
        {
            short identifier = reader.ReadInt16();
            short staticIdentifier = reader.ReadInt16();

            PropertyDeclaration pd = this.GetPropertyDeclaration(identifier);
            object staticResource = this.GetStaticResource(staticIdentifier);

            string prefix = this.LookupPrefix(XmlPIMapping.PresentationNamespace, false);
            string value = String.Format("{{{0}{1}StaticResource {2}}}", prefix, (String.IsNullOrEmpty(prefix)) ? String.Empty : ":", staticResource);

            XmlBamlProperty property = new XmlBamlProperty(PropertyType.Value, pd);
            property.Value = value;

            nodes.Enqueue(property);
        }


        private object GetStaticResource(short identifier)
        {
            // Recupero la risorsa nel gruppo corrente
            foreach (KeysResourcesCollection resource in keysResources)
            {
                // TODO: controllare. Se non lo trova nel gruppo corrente, va in quello successivo
                for (int x = 0; x < resource.Count; x++)
                {
                    KeysResource resourceGroup = resource[x];
                    if (resourceGroup.StaticResources.Count > identifier)
                        if (x > 0)
                            break;
                            //return "%" + resourceGroup.StaticResources[identifier] + "%";
                        else
                            return resourceGroup.StaticResources[identifier];
                }
            }

            //return "???";
            throw new ArgumentException("Cannot find StaticResource", "identifier");
        }

        private void ReadTextWithConverter()
        {
            string text = reader.ReadString();
            reader.ReadInt16();

            nodes.Enqueue(new XmlBamlText(text));
        }

        private void ReadTypeInfo()
        {
            short typeId = reader.ReadInt16();
            short assemblyId = reader.ReadInt16();
            string fullName = reader.ReadString();
            assemblyId = (short)(assemblyId & 0xfff);
            TypeDeclaration declaration;
            int length = fullName.LastIndexOf('.');
            if (length != -1)
            {
                string name = fullName.Substring(length + 1);
                string namespaceName = fullName.Substring(0, length);
                declaration = new TypeDeclaration(this, this.Resolver, name, namespaceName, assemblyId, false);
            }
            else
            {
                declaration = new TypeDeclaration(this, this.Resolver, fullName, string.Empty, assemblyId, false);
            }
            this.typeTable.Add(typeId, declaration);
        }

        private void ReadAssemblyInfo()
        {
            short key = reader.ReadInt16();
            string text = reader.ReadString();
            this.assemblyTable.Add(key, text);
        }

        private void ReadStringInfo()
        {
            short key = reader.ReadInt16();
            string text = reader.ReadString();
            this.stringTable.Add(key, text);
        }

        private TypeDeclaration GetTypeDeclaration(short identifier)
        {
            TypeDeclaration declaration;
            if (identifier >= 0)
                declaration = (TypeDeclaration)this.typeTable[identifier];
            else
                declaration = KnownInfo.KnownTypeTable[-identifier];

            if (declaration == null)
                throw new NotSupportedException();

            return declaration;
        }

        internal string GetAssembly(short identifier)
        {
            return this.assemblyTable[identifier].ToString();
        }

        private XmlBamlNode CurrentNode
        {
            get
            {
                return _currentNode;
            }
        }

        ///<summary>
        ///When overridden in a derived class, gets the namespace URI (as defined in the W3C Namespace specification) of the node on which the reader is positioned.
        ///</summary>
        ///
        ///<returns>
        ///The namespace URI of the current node; otherwise an empty string.
        ///</returns>
        ///
        public override string NamespaceURI
        {
            get
            {
                if (intoAttribute) return String.Empty;

                TypeDeclaration declaration;
                XmlBamlNode node = this.CurrentNode;
                if (node is XmlBamlProperty)
                {
                    declaration = ((XmlBamlProperty)node).PropertyDeclaration.DeclaringType;
                    TypeDeclaration elementDeclaration = this.readingElements.Peek().TypeDeclaration;

                    XmlPIMapping propertyMapping = FindByClrNamespaceAndAssemblyId(declaration) ?? XmlPIMapping.Presentation;
                    XmlPIMapping elementMapping = FindByClrNamespaceAndAssemblyId(elementDeclaration) ?? XmlPIMapping.Presentation;
                    if (String.CompareOrdinal(propertyMapping.XmlNamespace, elementMapping.XmlNamespace) == 0
                        || (elementDeclaration.Type != null && declaration.Type != null && elementDeclaration.Type.IsSubclassOf(declaration.Type)))
                        return String.Empty;
                }
                else if (node is XmlBamlElement)
                    declaration = ((XmlBamlElement)node).TypeDeclaration;
                else
                    return String.Empty;

                XmlPIMapping mapping = FindByClrNamespaceAndAssemblyId(declaration);
                if (mapping == null)
                    mapping = XmlPIMapping.Presentation;

                return mapping.XmlNamespace;
            }
        }

        ///<summary>
        ///When overridden in a derived class, gets the namespace prefix associated with the current node.
        ///</summary>
        ///
        ///<returns>
        ///The namespace prefix associated with the current node.
        ///</returns>
        ///
        public override string Prefix
        {
            get
            {
                if (!intoAttribute)
                    return ((IXmlNamespaceResolver)this).LookupPrefix(this.NamespaceURI) ?? String.Empty;
                return String.Empty;
            }
        }

        ///<summary>
        ///When overridden in a derived class, gets a value indicating whether the current node can have a <see cref="P:System.Xml.XmlReader.Value"></see>.
        ///</summary>
        ///
        ///<returns>
        ///true if the node on which the reader is currently positioned can have a Value; otherwise, false. If false, the node has a value of String.Empty.
        ///</returns>
        ///
        public override bool HasValue
        {
            get { return this.Value != null; }
        }

        /// <summary>
        /// Returns object used to resolve types
        /// </summary>
        public ITypeResolver Resolver
        {
            get { return _resolver; }
        }

        ///<summary>
        ///When overridden in a derived class, gets the text value of the current node.
        ///</summary>
        ///
        ///<returns>
        ///The value returned depends on the <see cref="P:System.Xml.XmlReader.NodeType"></see> of the node. The following table lists node types that have a value to return. All other node types return String.Empty.Node type Value AttributeThe value of the attribute. CDATAThe content of the CDATA section. CommentThe content of the comment. DocumentTypeThe internal subset. ProcessingInstructionThe entire content, excluding the target. SignificantWhitespaceThe white space between markup in a mixed content model. TextThe content of the text node. WhitespaceThe white space between markup. XmlDeclarationThe content of the declaration. 
        ///</returns>
        ///
        public override string Value
        {
            get
            {
                XmlBamlNode node = this.CurrentNode;
                if (node is XmlBamlProperty)
                    return ((XmlBamlProperty)node).Value.ToString();
                else if (node is XmlBamlText)
                    return ((XmlBamlText)node).Text;
                else if (node is XmlBamlElement)
                    return String.Empty;

                return String.Empty;
            }
        }

        /// <summary>
        /// Return root namespaces
        /// </summary>
        public IDictionary<string, string> RootNamespaces
        {
            get { return _rootNamespaces; }
        }

        ///<summary>
        ///When overridden in a derived class, gets the depth of the current node in the XML document.
        ///</summary>
        ///
        ///<returns>
        ///The depth of the current node in the XML document.
        ///</returns>
        ///
        public override int Depth
        {
            get { return this.readingElements.Count; }
        }

        ///<summary>
        ///When overridden in a derived class, gets the base URI of the current node.
        ///</summary>
        ///
        ///<returns>
        ///The base URI of the current node.
        ///</returns>
        ///
        public override string BaseURI
        {
            get { return String.Empty; }
        }

        ///<summary>
        ///When overridden in a derived class, gets a value indicating whether the current node is an empty element (for example, &lt;MyElement/&gt;).
        ///</summary>
        ///
        ///<returns>
        ///true if the current node is an element (<see cref="P:System.Xml.XmlReader.NodeType"></see> equals XmlNodeType.Element) that ends with /&gt;; otherwise, false.
        ///</returns>
        ///
        public override bool IsEmptyElement
        {
            get { return false; }
        }

        //public override bool IsDefault
        //{
        //    get
        //    {
        //        return this.NamespaceURI == null;
        //    }
        //}

        ///<summary>
        ///When overridden in a derived class, gets the number of attributes on the current node.
        ///</summary>
        ///
        ///<returns>
        ///The number of attributes on the current node.
        ///</returns>
        ///
        public override int AttributeCount
        {
            get { throw new NotImplementedException(); }
        }

        ///<summary>
        ///When overridden in a derived class, gets a value indicating whether the reader is positioned at the end of the stream.
        ///</summary>
        ///
        ///<returns>
        ///true if the reader is positioned at the end of the stream; otherwise, false.
        ///</returns>
        ///
        public override bool EOF
        {
            get { return _eof; }
        }

        ///<summary>
        ///When overridden in a derived class, gets the state of the reader.
        ///</summary>
        ///
        ///<returns>
        ///One of the <see cref="T:System.Xml.ReadState"></see> values.
        ///</returns>
        ///
        public override ReadState ReadState
        {
            get
            {
                if (!initialized)
                    return ReadState.Initial;
                else if (reader == null)
                    return ReadState.Closed;
                else if (this.EOF)
                    return ReadState.EndOfFile;
                else
                    return ReadState.Interactive;
            }
        }

        public List<XmlPIMapping> Mappings
        {
            get { return _mappings; }
        }

        ///<summary>
        ///When overridden in a derived class, gets the <see cref="T:System.Xml.XmlNameTable"></see> associated with this implementation.
        ///</summary>
        ///
        ///<returns>
        ///The XmlNameTable enabling you to get the atomized version of a string within the node.
        ///</returns>
        ///
        public override XmlNameTable NameTable
        {
            get { return _nameTable; }
        }

        #region IXmlNamespaceResolver Members

        ///<summary>
        ///Gets a collection of defined prefix-namespace Mappings that are currently in scope.
        ///</summary>
        ///
        ///<returns>
        ///An <see cref="T:System.Collections.IDictionary"></see> that contains the current in-scope namespaces.
        ///</returns>
        ///
        ///<param name="scope">An <see cref="T:System.Xml.XmlNamespaceScope"></see> value that specifies the type of namespace nodes to return.</param>
        IDictionary<string, string> IXmlNamespaceResolver.GetNamespacesInScope(XmlNamespaceScope scope)
        {
            XmlNamespaceCollection namespaces = readingElements.Peek().Namespaces;
            Dictionary<String, String> list = new Dictionary<string, string>();
            foreach (XmlNamespace ns in namespaces)
            {
                list.Add(ns.Prefix, ns.Namespace);
            }

            return list;
        }

        ///<summary>
        ///Gets the namespace URI mapped to the specified prefix.
        ///</summary>
        ///
        ///<returns>
        ///The namespace URI that is mapped to the prefix; null if the prefix is not mapped to a namespace URI.
        ///</returns>
        ///
        ///<param name="prefix">The prefix whose namespace URI you wish to find.</param>
        string IXmlNamespaceResolver.LookupNamespace(string prefix)
        {
            return this.LookupNamespace(prefix);
        }

        ///<summary>
        ///Gets the prefix that is mapped to the specified namespace URI.
        ///</summary>
        ///
        ///<returns>
        ///The prefix that is mapped to the namespace URI; null if the namespace URI is not mapped to a prefix.
        ///</returns>
        ///
        ///<param name="namespaceName">The namespace URI whose prefix you wish to find.</param>
        string IXmlNamespaceResolver.LookupPrefix(string namespaceName)
        {
            return this.LookupPrefix(namespaceName, true);
        }

        private string LookupPrefix(string namespaceName, bool useReading)
        {
            Stack<XmlBamlElement> elements;
            if (useReading)
                elements = readingElements;
            else
                elements = this.elements;

            if (elements.Count == 0) return null;
            XmlNamespaceCollection namespaces = elements.Peek().Namespaces;

            return LookupPrefix(namespaceName, namespaces);
        }

        private static string LookupPrefix(string namespaceName, XmlNamespaceCollection namespaces)
        {
            for (int x = 0; x < namespaces.Count; x++)
            {
                if (String.CompareOrdinal(namespaces[x].Namespace, namespaceName) == 0)
                    return namespaces[x].Prefix;
            }

            return null;
        }

        #endregion

        #region IntegerCollectionType

        internal enum IntegerCollectionType : byte
        {
            Byte = 2,
            Consecutive = 1,
            Integer = 4,
            Unknown = 0,
            UShort = 3
        }

        #endregion

        #region NodesCollection

        internal class NodesCollection : List<XmlBamlNode>
        {
            public XmlBamlNode Last
            {
                get
                {
                    if (this.Count > 0)
                    {
                        int i = this.Count - 1;
                        return this[i];
                    }
                    return null;
                }
            }

            public void RemoveLast()
            {
                if (this.Count > 0)
                    this.Remove(this.Last);
            }

            public XmlBamlNode Dequeue()
            {
                return DequeueInternal(true);
            }

            public XmlBamlNode Peek()
            {
                return DequeueInternal(false);
            }

            private XmlBamlNode DequeueInternal(bool remove)
            {
                if (this.Count > 0)
                {
                    XmlBamlNode node = this[0];
                    if (remove)
                        this.RemoveAt(0);
                    return node;
                }
                else
                    return null;
            }


            public void Enqueue(XmlBamlNode node)
            {
                this.Add(node);
            }
        }

        #endregion
    }
}

#endif