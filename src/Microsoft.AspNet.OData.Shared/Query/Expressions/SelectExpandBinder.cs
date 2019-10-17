// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNet.OData.Common;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNet.OData.Query.Expressions
{
    /// <summary>
    /// Applies the given <see cref="SelectExpandQueryOption"/> to the given <see cref="IQueryable"/>.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Class coupling acceptable.")]
    internal class SelectExpandBinder
    {
     //   private SelectExpandQueryOption _selectExpandQuery;
        private ODataQueryContext _context;
        private IEdmModel _model;
        private ODataQuerySettings _settings;
        private string _modelID;

        public SelectExpandBinder(ODataQuerySettings settings, SelectExpandQueryOption selectExpandQuery)
        {
            Contract.Assert(settings != null);
            Contract.Assert(selectExpandQuery != null);
            Contract.Assert(selectExpandQuery.Context != null);
            Contract.Assert(selectExpandQuery.Context.Model != null);
            Contract.Assert(settings.HandleNullPropagation != HandleNullPropagationOption.Default);

        //    _selectExpandQuery = selectExpandQuery;
            _context = selectExpandQuery.Context;
            _model = _context.Model;
            _modelID = ModelContainer.GetModelID(_model);
            _settings = settings;
        }

        public SelectExpandBinder(ODataQuerySettings settings, ODataQueryContext context)
        {
            Contract.Assert(settings != null);
            Contract.Assert(context != null);
            Contract.Assert(context.Model != null);
            Contract.Assert(settings.HandleNullPropagation != HandleNullPropagationOption.Default);

            _context = context;
            _model = _context.Model;
            _modelID = ModelContainer.GetModelID(_model);
            _settings = settings;
        }

        public static IQueryable Bind(IQueryable queryable, ODataQuerySettings settings, SelectExpandQueryOption selectExpandQuery)
        {
            Contract.Assert(queryable != null);
            Contract.Assert(selectExpandQuery != null);

            SelectExpandBinder binder = new SelectExpandBinder(settings, selectExpandQuery.Context);
            return binder.Bind(queryable, selectExpandQuery);
        }

        public static object Bind(object entity, ODataQuerySettings settings, SelectExpandQueryOption selectExpandQuery)
        {
            Contract.Assert(entity != null);
            Contract.Assert(selectExpandQuery != null);

            SelectExpandBinder binder = new SelectExpandBinder(settings, selectExpandQuery.Context);
            return binder.Bind(entity, selectExpandQuery);
        }

        private object Bind(object entity, SelectExpandQueryOption selectExpandQuery)
        {
            // Needn't to verify the input, that's done at upper level.
            LambdaExpression projectionLambda = GetProjectionLambda(selectExpandQuery);

            // TODO: cache this ?
            return projectionLambda.Compile().DynamicInvoke(entity);
        }

        private IQueryable Bind(IQueryable queryable, SelectExpandQueryOption selectExpandQuery)
        {
            // Needn't to verify the input, that's done at upper level.
            Type elementType = selectExpandQuery.Context.ElementClrType;

            LambdaExpression projectionLambda = GetProjectionLambda(selectExpandQuery);

            MethodInfo selectMethod = ExpressionHelperMethods.QueryableSelectGeneric.MakeGenericMethod(elementType, projectionLambda.Body.Type);
            return selectMethod.Invoke(null, new object[] { queryable, projectionLambda }) as IQueryable;
        }

        private LambdaExpression GetProjectionLambda(SelectExpandQueryOption selectExpandQuery)
        {
            Type elementType = selectExpandQuery.Context.ElementClrType;
            IEdmNavigationSource navigationSource = selectExpandQuery.Context.NavigationSource;
            ParameterExpression source = Expression.Parameter(elementType);

            // expression looks like -> new Wrapper { Instance = source , Properties = "...", Container = new PropertyContainer { ... } }
            Expression projectionExpression = ProjectElement(source, selectExpandQuery.SelectExpandClause, _context.ElementType as IEdmStructuredType, navigationSource);

            // expression looks like -> source => new Wrapper { Instance = source .... }
            LambdaExpression projectionLambdaExpression = Expression.Lambda(projectionExpression, source);

            return projectionLambdaExpression;
        }

        internal Expression ProjectAsWrapper(Expression source, SelectExpandClause selectExpandClause,
            IEdmStructuredType structuredType, IEdmNavigationSource navigationSource, ExpandedReferenceSelectItem expandedItem = null,
            int? modelBoundPageSize = null)
        {
            Type elementType;
            if (TypeHelper.IsCollection(source.Type, out elementType))
            {
                // new CollectionWrapper<ElementType> { Instance = source.Select(s => new Wrapper { ... }) };
                return ProjectCollection(source, elementType, selectExpandClause, structuredType, navigationSource, expandedItem,
                    modelBoundPageSize);
            }
            else
            {
                // new Wrapper { v1 = source.property ... }
                return ProjectElement(source, selectExpandClause, structuredType, navigationSource);
            }
        }

        internal Expression CreatePropertyNameExpression(IEdmStructuredType elementType, IEdmProperty property, Expression source)
        {
            Contract.Assert(elementType != null);
            Contract.Assert(property != null);
            Contract.Assert(source != null);

            IEdmStructuredType declaringType = property.DeclaringType;

            // derived property using cast
            if (elementType != declaringType)
            {
                Type originalType = EdmLibHelpers.GetClrType(elementType, _model);
                Type castType = EdmLibHelpers.GetClrType(declaringType, _model);
                if (castType == null)
                {
                    throw new ODataException(Error.Format(SRResources.MappingDoesNotContainResourceType, declaringType.FullTypeName()));
                }

                if (!castType.IsAssignableFrom(originalType))
                {
                    // Expression
                    //          source is navigationPropertyDeclaringType ? propertyName : null
                    return Expression.Condition(
                        test: Expression.TypeIs(source, castType),
                        ifTrue: Expression.Constant(property.Name),
                        ifFalse: Expression.Constant(null, typeof(string)));
                }
            }

            // Expression
            //          "propertyName"
            return Expression.Constant(property.Name);
        }

        internal Expression CreatePropertyValueExpression(IEdmStructuredType elementType, IEdmProperty property, Expression source, FilterClause filterClause)
        {
            Contract.Assert(elementType != null);
            Contract.Assert(property != null);
            Contract.Assert(source != null);

            // Expression: source = source as propertyDeclaringType
            if (elementType != property.DeclaringType)
            {
                Type castType = EdmLibHelpers.GetClrType(property.DeclaringType, _model);
                if (castType == null)
                {
                    throw new ODataException(Error.Format(SRResources.MappingDoesNotContainResourceType, property.DeclaringType.FullTypeName()));
                }

                source = Expression.TypeAs(source, castType);
            }

            // Expression:  source.Property
            string propertyName = EdmLibHelpers.GetClrPropertyName(property, _model);
            PropertyInfo propertyInfo = source.Type.GetProperty(propertyName);
            Expression propertyValue = Expression.Property(source, propertyInfo);
            Type nullablePropertyType = TypeHelper.ToNullable(propertyValue.Type);
            Expression nullablePropertyValue = ExpressionHelpers.ToNullable(propertyValue);

            if (filterClause != null)
            {
                bool isCollection = property.Type.IsCollection();

                IEdmTypeReference edmElementType = (isCollection ? property.Type.AsCollection().ElementType() : property.Type);
                Type clrElementType = EdmLibHelpers.GetClrType(edmElementType, _model);
                if (clrElementType == null)
                {
                    throw new ODataException(Error.Format(SRResources.MappingDoesNotContainResourceType, edmElementType.FullName()));
                }

                Expression filterResult = nullablePropertyValue;

                ODataQuerySettings querySettings = new ODataQuerySettings()
                {
                    HandleNullPropagation = HandleNullPropagationOption.True,
                };

                if (isCollection)
                {
                    Expression filterSource = nullablePropertyValue;

                    // TODO: Implement proper support for $select/$expand after $apply
                    Expression filterPredicate = FilterBinder.Bind(null, filterClause, clrElementType, _context, querySettings);
                    filterResult = Expression.Call(
                        ExpressionHelperMethods.EnumerableWhereGeneric.MakeGenericMethod(clrElementType),
                        filterSource,
                        filterPredicate);

                    nullablePropertyType = filterResult.Type;
                }
                else if (_settings.HandleReferenceNavigationPropertyExpandFilter)
                {
                    LambdaExpression filterLambdaExpression = FilterBinder.Bind(null, filterClause, clrElementType, _context, querySettings) as LambdaExpression;
                    if (filterLambdaExpression == null)
                    {
                        throw new ODataException(Error.Format(SRResources.ExpandFilterExpressionNotLambdaExpression, property.Name, "LambdaExpression"));
                    }

                    ParameterExpression filterParameter = filterLambdaExpression.Parameters.First();
                    Expression predicateExpression = new ReferenceNavigationPropertyExpandFilterVisitor(filterParameter, nullablePropertyValue).Visit(filterLambdaExpression.Body);

                    // create expression similar to: 'predicateExpression == true ? nullablePropertyValue : null'
                    filterResult = Expression.Condition(
                        test: predicateExpression,
                        ifTrue: nullablePropertyValue,
                        ifFalse: Expression.Constant(value: null, type: nullablePropertyType));
                }

                if (_settings.HandleNullPropagation == HandleNullPropagationOption.True)
                {
                    // create expression similar to: 'nullablePropertyValue == null ? null : filterResult'
                    nullablePropertyValue = Expression.Condition(
                        test: Expression.Equal(nullablePropertyValue, Expression.Constant(value: null)),
                        ifTrue: Expression.Constant(value: null, type: nullablePropertyType),
                        ifFalse: filterResult);
                }
                else
                {
                    nullablePropertyValue = filterResult;
                }
            }

            if (_settings.HandleNullPropagation == HandleNullPropagationOption.True)
            {
                // create expression similar to: 'source == null ? null : propertyValue'
                propertyValue = Expression.Condition(
                    test: Expression.Equal(source, Expression.Constant(value: null)),
                    ifTrue: Expression.Constant(value: null, type: nullablePropertyType),
                    ifFalse: nullablePropertyValue);
            }
            else
            {
                // need to cast this to nullable as EF would fail while materializing if the property is not nullable and source is null.
                propertyValue = nullablePropertyValue;
            }

            return propertyValue;
        }

        private class ReferenceNavigationPropertyExpandFilterVisitor : ExpressionVisitor
        {
            private Expression _source;
            private ParameterExpression _parameterExpression;

            public ReferenceNavigationPropertyExpandFilterVisitor(ParameterExpression parameterExpression, Expression source)
            {
                _source = source;
                _parameterExpression = parameterExpression;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node != _parameterExpression)
                {
                    throw new ODataException(Error.Format(SRResources.ReferenceNavigationPropertyExpandFilterVisitorUnexpectedParameter, node.Name));
                }

                return _source;
            }
        }

        // Generates the expression
        //      source => new Wrapper { Instance = source, Container = new PropertyContainer { ..expanded properties.. } }
        internal Expression ProjectElement(Expression source, SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmNavigationSource navigationSource)
        {
            Contract.Assert(source != null);

            Type elementType = source.Type;
            Type wrapperType = typeof(SelectExpandWrapper<>).MakeGenericType(elementType);
            List<MemberAssignment> wrapperTypeMemberAssignments = new List<MemberAssignment>();

            PropertyInfo wrapperProperty;
            Expression wrapperPropertyValueExpression;
            bool isInstancePropertySet = false;
            bool isTypeNamePropertySet = false;
            bool isContainerPropertySet = false;

            // Initialize property 'ModelID' on the wrapper class.
            // source = new Wrapper { ModelID = 'some-guid-id' }
            wrapperProperty = wrapperType.GetProperty("ModelID");
            wrapperPropertyValueExpression = _settings.EnableConstantParameterization ?
                LinqParameterContainer.Parameterize(typeof(string), _modelID) :
                Expression.Constant(_modelID);
            wrapperTypeMemberAssignments.Add(Expression.Bind(wrapperProperty, wrapperPropertyValueExpression));

            if (IsSelectAll(selectExpandClause))
            {
                // Initialize property 'Instance' on the wrapper class
                wrapperProperty = wrapperType.GetProperty("Instance");
                wrapperTypeMemberAssignments.Add(Expression.Bind(wrapperProperty, source));

                wrapperProperty = wrapperType.GetProperty("UseInstanceForProperties");
                wrapperTypeMemberAssignments.Add(Expression.Bind(wrapperProperty, Expression.Constant(true)));
                isInstancePropertySet = true;
            }
            else
            {
                // Initialize property 'TypeName' on the wrapper class as we don't have the instance.
                Expression typeName = CreateTypeNameExpression(source, structuredType, _model);
                if (typeName != null)
                {
                    isTypeNamePropertySet = true;
                    wrapperProperty = wrapperType.GetProperty("InstanceType");
                    wrapperTypeMemberAssignments.Add(Expression.Bind(wrapperProperty, typeName));
                }
            }

            // Initialize the property 'Container' on the wrapper class
            // source => new Wrapper { Container =  new PropertyContainer { .... } }
            if (selectExpandClause != null)
            {
                IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude;
                IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand;
                ISet<IEdmStructuralProperty> autoSelectedProperties;

                GetSelectExpandProperties(_model, structuredType, navigationSource, selectExpandClause, out propertiesToInclude, out propertiesToExpand, out autoSelectedProperties);

                bool isSelectingOpenTypeSegments = GetSelectsOpenTypeSegments(selectExpandClause, structuredType);

                if (propertiesToExpand != null || propertiesToInclude != null || autoSelectedProperties != null || isSelectingOpenTypeSegments)
                {
                    Expression propertyContainerCreation =
                        BuildPropertyContainer(source, structuredType, propertiesToExpand, propertiesToInclude, autoSelectedProperties, isSelectingOpenTypeSegments);

                    if (propertyContainerCreation != null)
                    {
                        wrapperProperty = wrapperType.GetProperty("Container");
                        Contract.Assert(wrapperProperty != null);

                        wrapperTypeMemberAssignments.Add(Expression.Bind(wrapperProperty, propertyContainerCreation));
                        isContainerPropertySet = true;
                    }
                }
            }

            Type wrapperGenericType = GetWrapperGenericType(isInstancePropertySet, isTypeNamePropertySet, isContainerPropertySet);
            wrapperType = wrapperGenericType.MakeGenericType(elementType);
            return Expression.MemberInit(Expression.New(wrapperType), wrapperTypeMemberAssignments);
        }

        /// <summary>
        /// Gets the $select and $expand properties from the given <see cref="SelectExpandClause"/>
        /// </summary>
        /// <param name="model">The Edm model.</param>
        /// <param name="elementType">The current structural type.</param>
        /// <param name="navigationSource">The current navigation source.</param>
        /// <param name="selectExpandClause">The given select and expand clause.</param>
        /// <param name="propertiesToInclude">The out properties to include at current level, could be null.</param>
        /// <param name="propertiesToExpand">The out properties to expand at current level, could be null.</param>
        /// <param name="autoSelectedProperties">The out auto selected properties to include at current level, could be null.</param>
        internal static void GetSelectExpandProperties(IEdmModel model, IEdmStructuredType elementType, IEdmNavigationSource navigationSource,
            SelectExpandClause selectExpandClause,
            out IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude,
            out IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand,
            out ISet<IEdmStructuralProperty> autoSelectedProperties)
        {
            Contract.Assert(selectExpandClause != null);

            // Properties to include includes all the properties selected or in the middle of a $select and $expand path.
            // for example: "$expand=abc/xyz/nav", "abc" and "xyz" are the middle properties that should be included.
            // meanwhile, "nav" is the property that should be expanded.
            propertiesToInclude = null;
            propertiesToExpand = null;
            autoSelectedProperties = null;

            var currentLevelPropertiesInclude = new Dictionary<IEdmStructuralProperty, SelectExpandIncludeProperty>();
            IEnumerable<SelectItem> selectedItems = selectExpandClause.SelectedItems;
            foreach (ExpandedReferenceSelectItem expandedItem in selectedItems.OfType<ExpandedReferenceSelectItem>())
            {
                // Verify and process the $expand path
                IList<ODataPathSegment> remainingSegments;
                ODataPathSegment firstPropertySegment = expandedItem.PathToNavigationProperty.ProcessExpandPath(out remainingSegments);

                PropertySegment firstStructuralPropertySegment = firstPropertySegment as PropertySegment;
                if (firstStructuralPropertySegment != null)
                {
                    // for example: $expand=abc/xyz, the remaining segments should never be null because at least the last navigation segment is there.
                    Contract.Assert(remainingSegments != null);

                    SelectExpandIncludeProperty newPropertySelectItem;
                    if (!currentLevelPropertiesInclude.TryGetValue(firstStructuralPropertySegment.Property, out newPropertySelectItem))
                    {
                        newPropertySelectItem = new SelectExpandIncludeProperty(firstStructuralPropertySegment, navigationSource);
                        currentLevelPropertiesInclude[firstStructuralPropertySegment.Property] = newPropertySelectItem;
                    }

                    newPropertySelectItem.AddSubExpandItem(remainingSegments, expandedItem);
                }
                else
                {
                    // for example: $expand=xyz, if we couldn't find a structural property in the path, it means we get the last navigation segment.
                    // So, the remaing segments should be null and the last segment should be "NavigationPropertySegment".
                    Contract.Assert(remainingSegments == null);

                    NavigationPropertySegment firstNavigationPropertySegment = firstPropertySegment as NavigationPropertySegment;
                    Contract.Assert(firstNavigationPropertySegment != null);

                    // Needn't add this navigation property into the include property.
                    // Because this navigation property will be included separately.
                    if (propertiesToExpand == null)
                    {
                        propertiesToExpand = new Dictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem>();
                    }

                    propertiesToExpand[firstNavigationPropertySegment.NavigationProperty] = expandedItem;
                }
            }

            if (!IsSelectAll(selectExpandClause))
            {
                // We should skip the below steps for "SelectAll".
                // because if selectAll equals to true, we use the instance (the object) directly, not use the wrapper.
                foreach (var pathSelectItem in selectedItems.OfType<PathSelectItem>())
                {
                    // Verify and process the $select path
                    IList<ODataPathSegment> remainingSegments;
                    ODataPathSegment firstPropertySegment = pathSelectItem.SelectedPath.ProcessSelectPath(out remainingSegments);

                    PropertySegment firstSturucturalPropertySegment = firstPropertySegment as PropertySegment;
                    if (firstSturucturalPropertySegment != null)
                    {
                        SelectExpandIncludeProperty newPropertySelectItem;
                        if (!currentLevelPropertiesInclude.TryGetValue(firstSturucturalPropertySegment.Property, out newPropertySelectItem))
                        {
                            newPropertySelectItem = new SelectExpandIncludeProperty(firstSturucturalPropertySegment, navigationSource);
                            currentLevelPropertiesInclude[firstSturucturalPropertySegment.Property] = newPropertySelectItem;
                        }

                        newPropertySelectItem.AddSubSelectItem(remainingSegments, pathSelectItem);
                    }
                    else
                    {
                        // Do nothing here, because if we can't find a PropertySegment, the $select path maybe selecting an operation, or dynamic property.
                        // We needn't process the operation selection, and dynamic property is processed individually.
                    }
                }

                // We should include the keys if it's an entity.
                IEdmEntityType entityType = elementType as IEdmEntityType;
                if (entityType != null)
                {
                    foreach (IEdmStructuralProperty keyProperty in entityType.Key())
                    {
                        if (!currentLevelPropertiesInclude.Keys.Contains(keyProperty))
                        {
                            if (autoSelectedProperties == null)
                            {
                                autoSelectedProperties = new HashSet<IEdmStructuralProperty>();
                            }

                            autoSelectedProperties.Add(keyProperty);
                        }
                    }
                }

                // We should add concurrency properties, if not added
                if (navigationSource != null && model != null)
                {
                    IEnumerable<IEdmStructuralProperty> concurrencyProperties = model.GetConcurrencyProperties(navigationSource);
                    foreach (IEdmStructuralProperty concurrencyProperty in concurrencyProperties)
                    {
                        if (!currentLevelPropertiesInclude.Keys.Contains(concurrencyProperty))
                        {
                            if (autoSelectedProperties == null)
                            {
                                autoSelectedProperties = new HashSet<IEdmStructuralProperty>();
                            }

                            autoSelectedProperties.Add(concurrencyProperty);
                        }
                    }
                }
            }

            if (currentLevelPropertiesInclude.Any())
            {
                propertiesToInclude = new Dictionary<IEdmStructuralProperty, PathSelectItem>();
                foreach (var propertiesInclude in currentLevelPropertiesInclude)
                {
                    propertiesToInclude[propertiesInclude.Key] = propertiesInclude.Value == null ? null : propertiesInclude.Value.ToPathSelectItem();
                }
            }
        }

        private static bool GetSelectsOpenTypeSegments(SelectExpandClause selectExpandClause, IEdmStructuredType structuredType)
        {
            if (structuredType == null || !structuredType.IsOpen)
            {
                return false;
            }

            if (IsSelectAll(selectExpandClause))
            {
                return true;
            }

            return selectExpandClause.SelectedItems.OfType<PathSelectItem>().Any(x => x.SelectedPath.LastSegment is DynamicPathSegment);
        }

        private Expression CreateTotalCountExpression(Expression source, bool? countOption)
        {
            Expression countExpression = Expression.Constant(null, typeof(long?));
            if (countOption == null || !countOption.Value)
            {
                return countExpression;
            }

            Type elementType;
            if (!TypeHelper.IsCollection(source.Type, out elementType))
            {
                return countExpression;
            }

            MethodInfo countMethod;
            if (typeof(IQueryable).IsAssignableFrom(source.Type))
            {
                countMethod = ExpressionHelperMethods.QueryableCountGeneric.MakeGenericMethod(elementType);
            }
            else
            {
                countMethod = ExpressionHelperMethods.EnumerableCountGeneric.MakeGenericMethod(elementType);
            }

            // call Count() method.
            countExpression = Expression.Call(null, countMethod, new[] { source });

            if (_settings.HandleNullPropagation == HandleNullPropagationOption.True)
            {
                // source == null ? null : countExpression
                return Expression.Condition(
                       test: Expression.Equal(source, Expression.Constant(null)),
                       ifTrue: Expression.Constant(null, typeof(long?)),
                       ifFalse: ExpressionHelpers.ToNullable(countExpression));
            }
            else
            {
                return countExpression;
            }
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Class coupling acceptable")]
        private Expression BuildPropertyContainer(Expression source, IEdmStructuredType elementType,
            IDictionary<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertiesToExpand,
            IDictionary<IEdmStructuralProperty, PathSelectItem> propertiesToInclude,
            ISet<IEdmStructuralProperty> autoSelectedProperties,
            bool isSelectingOpenTypeSegments)
        {
            IList<NamedPropertyExpression> includedProperties = new List<NamedPropertyExpression>();

            if (propertiesToExpand != null)
            {
                foreach (KeyValuePair<IEdmNavigationProperty, ExpandedReferenceSelectItem> kvp in propertiesToExpand)
                {
                    // $expand=abc or $expand=abc/$ref
                    IEdmNavigationProperty propertyToExpand = kvp.Key;
                    ExpandedReferenceSelectItem expandItem = kvp.Value;

                    SelectExpandClause subSelectExpandClause = GetOrCreateSelectExpandClause(kvp);

                    ModelBoundQuerySettings querySettings = EdmLibHelpers.GetModelBoundQuerySettings(propertyToExpand, propertyToExpand.ToEntityType(), _model);

                    Expression propertyName = CreatePropertyNameExpression(elementType, propertyToExpand, source);

                    // TODO: Process $apply and $compute in the $expand ahead.
                    Expression propertyValue = CreatePropertyValueExpression(elementType, propertyToExpand, source, expandItem.FilterOption);

                    Expression nullCheck = GetNullCheckExpression(propertyToExpand, propertyValue, subSelectExpandClause);

                    Expression countExpression = CreateTotalCountExpression(propertyValue, expandItem.CountOption);

                    // projection can be null if the expanded navigation property is not further projected or expanded.
                    if (subSelectExpandClause != null)
                    {
                        int? modelBoundPageSize = querySettings == null ? null : querySettings.PageSize;
                        propertyValue = ProjectAsWrapper(propertyValue, subSelectExpandClause, propertyToExpand.ToEntityType(), expandItem.NavigationSource, expandItem, modelBoundPageSize);
                    }

                    NamedPropertyExpression propertyExpression = new NamedPropertyExpression(propertyName, propertyValue);
                    if (subSelectExpandClause != null)
                    {
                        if (!propertyToExpand.Type.IsCollection())
                        {
                            propertyExpression.NullCheck = nullCheck;
                        }
                        else if (_settings.PageSize.HasValue)
                        {
                            propertyExpression.PageSize = _settings.PageSize.Value;
                        }
                        else
                        {
                            if (querySettings != null && querySettings.PageSize.HasValue)
                            {
                                propertyExpression.PageSize = querySettings.PageSize.Value;
                            }
                        }

                        propertyExpression.TotalCount = countExpression;
                        propertyExpression.CountOption = expandItem.CountOption;
                    }

                    includedProperties.Add(propertyExpression);
                }
            }

            if (propertiesToInclude != null)
            {
                foreach (var propertyToInclude in propertiesToInclude)
                {
                    // $select=abc($select=...,$filter=...,$compute=...)....
                    IEdmStructuralProperty structuralProperty = propertyToInclude.Key;
                    PathSelectItem pathSelectItem = propertyToInclude.Value;

                    // get the property name expression
                    Expression propertyName = CreatePropertyNameExpression(elementType, structuralProperty, source);

                    Expression propertyValue;
                    if (pathSelectItem == null)
                    {
                        propertyValue = CreatePropertyValueExpression(elementType, structuralProperty, source, filterClause: null);
                        includedProperties.Add(new NamedPropertyExpression(propertyName, propertyValue));
                        continue;
                    }

                    // TODO: Process $compute in the $select ahead.
                    propertyValue = CreatePropertyValueExpression(elementType, structuralProperty, source, pathSelectItem.FilterOption);

                    Expression countExpression = CreateTotalCountExpression(propertyValue, pathSelectItem.CountOption);

                    ModelBoundQuerySettings querySettings = EdmLibHelpers.GetModelBoundQuerySettings(structuralProperty, structuralProperty.Type.ToStructuredType(), _context.Model);

                    // subSelectExpandClause can be null if the expanded navigation property is not further projected or expanded.
                    SelectExpandClause subSelectExpandClause = pathSelectItem.SelectAndExpand;
                    if (subSelectExpandClause != null)
                    {
                        int? modelBoundPageSize = querySettings == null ? null : querySettings.PageSize;
                        propertyValue = ProjectAsWrapper(propertyValue, subSelectExpandClause, structuralProperty.Type.ToStructuredType(), pathSelectItem.NavigationSource, null, modelBoundPageSize);
                    }

                    NamedPropertyExpression propertyExpression = new NamedPropertyExpression(propertyName, propertyValue);
                    if (subSelectExpandClause != null)
                    {
                        if (!structuralProperty.Type.IsCollection())
                        {
                            // propertyExpression.NullCheck = nullCheck;
                        }
                        else if (_settings.PageSize.HasValue)
                        {
                            propertyExpression.PageSize = _settings.PageSize.Value;
                        }
                        else
                        {
                            if (querySettings != null && querySettings.PageSize.HasValue)
                            {
                                propertyExpression.PageSize = querySettings.PageSize.Value;
                            }
                        }

                        propertyExpression.TotalCount = countExpression;
                        propertyExpression.CountOption = pathSelectItem.CountOption;
                    }

                    includedProperties.Add(propertyExpression);
                }
            }

            if (autoSelectedProperties != null)
            {
                foreach (IEdmStructuralProperty propertyToInclude in autoSelectedProperties)
                {
                    Expression propertyName = CreatePropertyNameExpression(elementType, propertyToInclude, source);
                    Expression propertyValue = CreatePropertyValueExpression(elementType, propertyToInclude, source, filterClause: null);

                    includedProperties.Add(new NamedPropertyExpression(propertyName, propertyValue)
                    {
                        AutoSelected = true
                    });
                }
            }

            if (isSelectingOpenTypeSegments)
            {
                var dynamicPropertyDictionary = EdmLibHelpers.GetDynamicPropertyDictionary(elementType, _model);
                if (dynamicPropertyDictionary != null)
                {
                    Expression propertyName = Expression.Constant(dynamicPropertyDictionary.Name);
                    Expression propertyValue = Expression.Property(source, dynamicPropertyDictionary.Name);
                    Expression nullablePropertyValue = ExpressionHelpers.ToNullable(propertyValue);
                    if (_settings.HandleNullPropagation == HandleNullPropagationOption.True)
                    {
                        // source == null ? null : propertyValue
                        propertyValue = Expression.Condition(
                            test: Expression.Equal(source, Expression.Constant(value: null)),
                            ifTrue: Expression.Constant(value: null, type: TypeHelper.ToNullable(propertyValue.Type)),
                            ifFalse: nullablePropertyValue);
                    }
                    else
                    {
                        propertyValue = nullablePropertyValue;
                    }

                    includedProperties.Add(new NamedPropertyExpression(propertyName, propertyValue));
                }
            }

            // create a property container that holds all these property names and values.
            return PropertyContainer.CreatePropertyContainer(includedProperties);
        }

        private static SelectExpandClause GetOrCreateSelectExpandClause(KeyValuePair<IEdmNavigationProperty, ExpandedReferenceSelectItem> propertyToExpand)
        {
            // for normal $expand=....
            ExpandedNavigationSelectItem expandNavigationSelectItem = propertyToExpand.Value as ExpandedNavigationSelectItem;
            if (expandNavigationSelectItem != null)
            {
                return expandNavigationSelectItem.SelectAndExpand;
            }

            // for $expand=..../$ref, just includes the keys properties
            IList<SelectItem> selectItems = new List<SelectItem>();
            foreach (IEdmStructuralProperty keyProperty in propertyToExpand.Key.ToEntityType().Key())
            {
                selectItems.Add(new PathSelectItem(new ODataSelectPath(new PropertySegment(keyProperty))));
            }

            return new SelectExpandClause(selectItems, false);
        }

        private Expression AddOrderByQueryForSource(Expression source, OrderByClause orderbyClause, Type elementType)
        {
            if (orderbyClause != null)
            {
                // TODO: Implement proper support for $select/$expand after $apply
                ODataQuerySettings querySettings = new ODataQuerySettings()
                {
                    HandleNullPropagation = HandleNullPropagationOption.True,
                };

                LambdaExpression orderByExpression =
                    FilterBinder.Bind(null, orderbyClause, elementType, _context, querySettings);
                source = ExpressionHelpers.OrderBy(source, orderByExpression, elementType, orderbyClause.Direction);
            }

            return source;
        }

        private Expression GetNullCheckExpression(IEdmNavigationProperty propertyToExpand, Expression propertyValue,
            SelectExpandClause projection)
        {
            if (projection == null || propertyToExpand.Type.IsCollection())
            {
                return null;
            }

            if (IsSelectAll(projection) || !propertyToExpand.ToEntityType().Key().Any())
            {
                return Expression.Equal(propertyValue, Expression.Constant(null));
            }

            Expression keysNullCheckExpression = null;
            foreach (var key in propertyToExpand.ToEntityType().Key())
            {
                var propertyValueExpression = CreatePropertyValueExpression(propertyToExpand.ToEntityType(), key, propertyValue, filterClause: null);
                var keyExpression = Expression.Equal(
                    propertyValueExpression,
                    Expression.Constant(null, propertyValueExpression.Type));

                keysNullCheckExpression = keysNullCheckExpression == null
                    ? keyExpression
                    : Expression.And(keysNullCheckExpression, keyExpression);
            }

            return keysNullCheckExpression;
        }

        // new CollectionWrapper<ElementType> { Instance = source.Select((ElementType element) => new Wrapper { }) }
        private Expression ProjectCollection(Expression source, Type elementType, SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmNavigationSource navigationSource, ExpandedReferenceSelectItem expandedItem, int? modelBoundPageSize)
        {
            ParameterExpression element = Expression.Parameter(elementType);

            // expression
            //      new Wrapper { }
            Expression projection = ProjectElement(element, selectExpandClause, structuredType, navigationSource);

            // expression
            //      (ElementType element) => new Wrapper { }
            LambdaExpression selector = Expression.Lambda(projection, element);

            if (expandedItem != null)
            {
                source = AddOrderByQueryForSource(source, expandedItem.OrderByOption, elementType);
            }

            IEdmEntityType entityType = structuredType as IEdmEntityType;
            if (entityType != null)
            {
                if (_settings.PageSize.HasValue || modelBoundPageSize.HasValue ||
                    (expandedItem != null && (expandedItem.TopOption.HasValue || expandedItem.SkipOption.HasValue)))
                {
                    // nested paging. Need to apply order by first, and take one more than page size as we need to know
                    // whether the collection was truncated or not while generating next page links.
                    IEnumerable<IEdmStructuralProperty> properties =
                        entityType.Key().Any()
                            ? entityType.Key()
                            : entityType
                                .StructuralProperties()
                                .Where(property => property.Type.IsPrimitive() && !property.Type.IsStream())
                                .OrderBy(property => property.Name);

                    if (expandedItem == null || expandedItem.OrderByOption == null)
                    {
                        bool alreadyOrdered = false;
                        foreach (var prop in properties)
                        {
                            source = ExpressionHelpers.OrderByPropertyExpression(source, prop.Name, elementType,
                                alreadyOrdered);
                            if (!alreadyOrdered)
                            {
                                alreadyOrdered = true;
                            }
                        }
                    }

                    if (expandedItem != null && expandedItem.SkipOption.HasValue)
                    {
                        Contract.Assert(expandedItem.SkipOption.Value <= Int32.MaxValue);
                        source = ExpressionHelpers.Skip(source, (int)expandedItem.SkipOption.Value, elementType,
                            _settings.EnableConstantParameterization);
                    }

                    if (expandedItem != null && expandedItem.TopOption.HasValue)
                    {
                        Contract.Assert(expandedItem.TopOption.Value <= Int32.MaxValue);
                        source = ExpressionHelpers.Take(source, (int)expandedItem.TopOption.Value, elementType,
                            _settings.EnableConstantParameterization);
                    }

                    // don't page nested collections if EnableCorrelatedSubqueryBuffering is enabled
                    if (expandedItem == null || !_settings.EnableCorrelatedSubqueryBuffering)
                    {
                        if (_settings.PageSize.HasValue)
                        {
                            source = ExpressionHelpers.Take(source, _settings.PageSize.Value + 1, elementType,
                                _settings.EnableConstantParameterization);
                        }
                        else if (_settings.ModelBoundPageSize.HasValue)
                        {
                            source = ExpressionHelpers.Take(source, modelBoundPageSize.Value + 1, elementType,
                                _settings.EnableConstantParameterization);
                        }
                    }
                }
            }

            // expression
            //      source.Select((ElementType element) => new Wrapper { })
            var selectMethod = GetSelectMethod(elementType, projection.Type);
            Expression selectedExpresion = Expression.Call(selectMethod, source, selector);

            // Append ToList() to collection as a hint to LINQ provider to buffer correlated subqueries in memory and avoid executing N+1 queries
            if (_settings.EnableCorrelatedSubqueryBuffering)
            {
                selectedExpresion = Expression.Call(ExpressionHelperMethods.QueryableToList.MakeGenericMethod(projection.Type), selectedExpresion);
            }

            if (_settings.HandleNullPropagation == HandleNullPropagationOption.True)
            {
                // source == null ? null : projectedCollection
                return Expression.Condition(
                       test: Expression.Equal(source, Expression.Constant(null)),
                       ifTrue: Expression.Constant(null, selectedExpresion.Type),
                       ifFalse: selectedExpresion);
            }
            else
            {
                return selectedExpresion;
            }
        }

        // OData formatter requires the type name of the entity that is being written if the type has derived types.
        // Expression
        //      source is GrandChild ? "GrandChild" : ( source is Child ? "Child" : "Root" )
        // Notice that the order is important here. The most derived type must be the first to check.
        // If entity framework had a way to figure out the type name without selecting the whole object, we don't have to do this magic.
        internal static Expression CreateTypeNameExpression(Expression source, IEdmStructuredType elementType, IEdmModel model)
        {
            IReadOnlyList<IEdmStructuredType> derivedTypes = GetAllDerivedTypes(elementType, model);
            if (derivedTypes.Count == 0)
            {
                // no inheritance.
                return null;
            }
            else
            {
                Expression expression = Expression.Constant(elementType.FullTypeName());
                for (int i = 0; i < derivedTypes.Count; i++)
                {
                    Type clrType = EdmLibHelpers.GetClrType(derivedTypes[i], model);
                    if (clrType == null)
                    {
                        throw new ODataException(Error.Format(SRResources.MappingDoesNotContainResourceType, derivedTypes[0].FullTypeName()));
                    }

                    expression = Expression.Condition(
                                    test: Expression.TypeIs(source, clrType),
                                    ifTrue: Expression.Constant(derivedTypes[i].FullTypeName()),
                                    ifFalse: expression);
                }

                return expression;
            }
        }

        // returns all the derived types (direct and indirect) of baseType ordered according to their depth. The direct children
        // are the first in the list.
        private static IReadOnlyList<IEdmStructuredType> GetAllDerivedTypes(IEdmStructuredType baseType, IEdmModel model)
        {
            IEnumerable<IEdmStructuredType> allStructuredTypes = model.SchemaElements.OfType<IEdmStructuredType>();

            List<Tuple<int, IEdmStructuredType>> derivedTypes = new List<Tuple<int, IEdmStructuredType>>();
            foreach (IEdmStructuredType structuredType in allStructuredTypes)
            {
                int distance = IsDerivedTypeOf(structuredType, baseType);
                if (distance > 0)
                {
                    derivedTypes.Add(Tuple.Create(distance, structuredType));
                }
            }

            return derivedTypes.OrderBy(tuple => tuple.Item1).Select(tuple => tuple.Item2).ToList();
        }

        // returns -1 if type does not derive from baseType and a positive number representing the distance
        // between them if it does.
        private static int IsDerivedTypeOf(IEdmStructuredType type, IEdmStructuredType baseType)
        {
            int distance = 0;
            while (type != null)
            {
                if (baseType == type)
                {
                    return distance;
                }

                type = type.BaseType();
                distance++;
            }

            return -1;
        }

        private static MethodInfo GetSelectMethod(Type elementType, Type resultType)
        {
            return ExpressionHelperMethods.EnumerableSelectGeneric.MakeGenericMethod(elementType, resultType);
        }

        private static bool IsSelectAll(SelectExpandClause selectExpandClause)
        {
            if (selectExpandClause == null)
            {
                return true;
            }

            if (selectExpandClause.AllSelected || selectExpandClause.SelectedItems.OfType<WildcardSelectItem>().Any())
            {
                return true;
            }

            return false;
        }

        private static Type GetWrapperGenericType(bool isInstancePropertySet, bool isTypeNamePropertySet, bool isContainerPropertySet)
        {
            if (isInstancePropertySet)
            {
                // select all
                Contract.Assert(!isTypeNamePropertySet, "we don't set type name if we set instance as it can be figured from instance");

                return isContainerPropertySet ? typeof(SelectAllAndExpand<>) : typeof(SelectAll<>);
            }
            else
            {
                Contract.Assert(isContainerPropertySet, "if it is not select all, container should hold something");

                return isTypeNamePropertySet ? typeof(SelectSomeAndInheritance<>) : typeof(SelectSome<>);
            }
        }

        /* Entityframework requires that the two different type initializers for a given type in the same query have the
        same set of properties in the same order.

        A ~/People?$select=Name&$expand=Friend results in a select expression that has two SelectExpandWrapper<Person>
        expressions, one for the root level person and the second for the expanded Friend person.
        The first wrapper has the Container property set (contains Name and Friend values) where as the second wrapper
        has the Instance property set as it contains all the properties of the expanded person.

        The below four classes workaround that entity framework limitation by defining a seperate type for each
        property selection combination possible. */

        private class SelectAllAndExpand<TEntity> : SelectExpandWrapper<TEntity>
        {
        }

        private class SelectAll<TEntity> : SelectExpandWrapper<TEntity>
        {
        }

        private class SelectSomeAndInheritance<TEntity> : SelectExpandWrapper<TEntity>
        {
        }

        private class SelectSome<TEntity> : SelectAllAndExpand<TEntity>
        {
        }
    }
}
