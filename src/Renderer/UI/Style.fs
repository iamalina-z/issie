module DiagramStyle

open Fable.React.Props

let private headerHeight = "52px"
let private rightSectionWidthS = "400px" // Small right section.
let private rightSectionWidthL = "650px" // Large right section.

let private leftSectionStyle widthRightSec = Style [
    Width (sprintf "calc(100%s - %s)" "%" widthRightSec)
]

let navbarStyle model = Style [
    Width "100%"
    Height headerHeight
]

let private rightSectionStyle width = Style [
    Position PositionOptions.Fixed
    Right "0px"
    Top "0px"
    Height  "100%" //(sprintf "calc(100%s - %s)" "%" headerHeight) // WindowSize - headerHeight
    Width width
    OverflowX OverflowOptions.Hidden
    OverflowY OverflowOptions.Scroll
    BorderTop "2px solid lightgray"
]

/// Style when right column is expanded.
let rightSectionStyleS = rightSectionStyle rightSectionWidthS
let leftSectionStyleL = leftSectionStyle rightSectionWidthS
/// Style when right column is small.
let rightSectionStyleL = rightSectionStyle rightSectionWidthL
let leftSectionStyleS = leftSectionStyle rightSectionWidthL

let canvasHiddenStyle = Style [
    Display DisplayOptions.None
]

let private canvasVisibleStyle right = Style [
    Display DisplayOptions.Block
    Position PositionOptions.Absolute // Required to work.
    OverflowX OverflowOptions.Scroll
    OverflowY OverflowOptions.Scroll
    Resize "horizontal"
    Top headerHeight // Placed just under the header.
    Left "0px"
    Bottom "0px"
    Right right
    BorderTop "2px solid lightgray"
]

/// Style when right column is expanded.
let canvasVisibleStyleS = canvasVisibleStyle rightSectionWidthL
/// Style when right column is small.
let canvasVisibleStyleL = canvasVisibleStyle rightSectionWidthS

let canvasSmallMenuStyle = Style [
    Display DisplayOptions.Block
    Position PositionOptions.Absolute // Required to work.
    OverflowX OverflowOptions.Hidden
    OverflowY OverflowOptions.Hidden
    Left "10px"
    Bottom "25px"
    Right (sprintf "calc(100%s - 300px)" "%")
]

let canvasSmallButtonStyle = Style [
    MarginRight "5px"
    BackgroundColor "white"
    BorderRadius "4px"
    BorderStyle "solid"
    Outline "none"
    Padding "4px"
    Opacity 0.7
]

let notificationStyle = Style [
    ZIndex 100 // In front of everything.
    Position PositionOptions.Absolute
    Right "20px"
    Bottom "20px"
]

let simulationNumberStyle = Style [
    Width "320px"
    Height "30px"
]

let simulationBitStyle = Style [
    Width "100px"
    Height "30px"
    PaddingTop "3px"
]

let menuLabelStyle = Style [
    Outline "none"
    MarginTop "10px"
    MarginBottom "10px"
    Color "#7a7a7a"
    FontSize "0.80em"
    LetterSpacing "0.1em"
    TextTransform "uppercase"
]
