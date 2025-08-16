# Collection-Based Training Implementation

## Overview

This implementation adds collection-based training to the Daminion TorchTrainer, allowing users to select specific collections from their Daminion server as training data sources instead of using search queries.

## Features Added

### 1. UI Enhancements
- **Data Source Type Selection**: Radio buttons to choose between "Search Query" and "Collection"
- **Collection ComboBox**: Dropdown list showing all available collections from the Daminion server
- **Refresh Button**: Manual refresh of collections list
- **Collection Information Display**: Shows selected collection details

### 2. Backend Implementation

#### New Models
- `CollectionSelectionItem`: Represents a collection for UI binding
  - `Text`: Display name
  - `Value`: Collection ID
  - `Guid`: Collection GUID
  - `Description`: Additional metadata

#### ViewModel Enhancements
- **Collection Properties**:
  - `UseSearchQuery` / `UseCollection`: Radio button binding
  - `AvailableCollections`: Observable collection of available collections
  - `SelectedCollection`: Currently selected collection
  - `SelectedCollectionText`: Display text for selected collection

- **New Commands**:
  - `RefreshCollectionsCommand`: Refreshes the collections list from Daminion

- **Collection Management Methods**:
  - `RefreshCollectionsAsync()`: Fetches collections from Daminion API
  - `UpdateSelectedCollectionText()`: Updates the display text

#### Data Extraction Service
- **New Method**: `ExtractTrainingDataFromCollectionAsync()`
  - Takes a collection ID as parameter
  - Uses Daminion API to find the Collections tag
  - Searches for media items in the specific collection
  - Processes images and metadata same as search-based extraction

### 3. API Integration

#### Daminion API Usage
The implementation leverages existing Daminion API endpoints:

1. **Get Tags** (`/api/settings/getTags`): Finds the "Shared Collections" or "Collections" tag
2. **Get Tag Values** (`/api/indexedTagValues/getIndexedTagValues`): Retrieves all collection values
3. **Search Media Items** (`/api/mediaItems/get`): Searches for items in a specific collection using query format: `{collectionsTagId},{collectionId}`

#### Collection Search Format
Based on the API documentation, collections are searched using:
```
queryLine={collectionsTagId},{collectionId}
```

Where:
- `collectionsTagId` is the ID of the "Shared Collections" tag
- `collectionId` is the specific collection's ID

### 4. Settings Persistence
- **Registry Storage**: Collection preferences are saved to Windows Registry
- **Auto-Restore**: Selected collection is restored on application startup
- **Settings Properties**:
  - `UseSearchQuery` / `UseCollection`: Data source type preference
  - `SelectedCollectionId`: Saved collection ID
  - `SelectedCollectionText`: Saved collection name
  - `SelectedCollectionGuid`: Saved collection GUID

## Implementation Details

### Collection Discovery Process
1. **Connect to Daminion**: User connects to Daminion server
2. **Auto-Refresh**: Collections are automatically loaded on connection
3. **Tag Discovery**: System finds the "Shared Collections" tag
4. **Value Retrieval**: All collection values are fetched
5. **UI Population**: Collections are displayed in the ComboBox

### Data Extraction Process
1. **Collection Selection**: User selects a collection from the dropdown
2. **Validation**: System ensures a collection is selected when in collection mode
3. **API Query**: Creates search query using collection ID
4. **Media Retrieval**: Fetches all media items in the collection
5. **Processing**: Same image processing pipeline as search-based extraction

### Error Handling
- **Connection Required**: Collections can only be refreshed when connected
- **Tag Not Found**: Graceful handling if Collections tag doesn't exist
- **Empty Collections**: Handles collections with no media items
- **API Errors**: Comprehensive error logging and user feedback

## Usage Instructions

### For Users
1. **Connect to Daminion**: Enter server URL, username, and password
2. **Select Data Source Type**: Choose "Collection" radio button
3. **Select Collection**: Choose from the dropdown list of available collections
4. **Extract Data**: Click "Extract Data" to load images from the collection
5. **Train Model**: Proceed with training as usual

### For Developers
The implementation follows the existing patterns:
- **MVVM Architecture**: Proper separation of concerns
- **Async/Await**: Non-blocking UI operations
- **Logging**: Comprehensive Serilog integration
- **Error Handling**: Graceful degradation and user feedback
- **Settings Persistence**: Registry-based configuration storage

## Technical Considerations

### API Compatibility
- **Daminion API v4**: Uses the documented API endpoints
- **Collection Tag Names**: Supports both "Shared Collections" and "Collections" tag names
- **Query Format**: Follows the documented queryLine parameter format

### Performance
- **Lazy Loading**: Collections are only loaded when needed
- **Caching**: Collection list is cached in memory
- **Progress Reporting**: Real-time progress updates during extraction

### Scalability
- **Large Collections**: Handles collections with thousands of items
- **Memory Management**: Efficient processing of large datasets
- **Batch Processing**: Processes images in manageable batches

## Future Enhancements

### Potential Improvements
1. **Collection Filtering**: Search/filter within collections
2. **Multiple Collections**: Support for training on multiple collections
3. **Collection Metadata**: Display collection statistics (item count, etc.)
4. **Collection Preview**: Show sample images from selected collection
5. **Collection Management**: Create/edit collections directly in the trainer

### API Extensions
1. **Collection Creation**: API endpoints for creating new collections
2. **Collection Statistics**: Get collection metadata and statistics
3. **Collection Search**: Search within collections by metadata

## Conclusion

This implementation provides a more targeted and efficient approach to training data selection by leveraging Daminion's collection system. Users can now train models on specific, curated sets of images rather than relying on broad search queries, leading to more focused and effective training results.

The implementation is fully integrated with the existing codebase, maintains backward compatibility, and follows established patterns for maintainability and extensibility.
