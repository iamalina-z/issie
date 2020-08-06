(*
    CatalogueView.fs

    View for catalogue in the right tab.
*)

module CatalogueView

open Fulma
open Fable.React
open Fable.React.Props

open Helpers
open DiagramStyle
open DiagramModelType
open DiagramMessageType
open CommonTypes
open PopupView

type Bbox = {
    LTop: (int*int)
    RBot: (int*int)
    }

/// Current Draw2D schematic sheet scroll and zoom position.
/// Component positions are always set or returned as unscaled, and with no offset.
/// Sheet values, offsets, and scaling change what portion of draw2d canvas is seen.

type ScrollPos = {
    /// X view size in unzoomed pixels
    SheetX: int
    /// Y view size in unzoomed pixels
    SheetY: int
    /// X view leftmost offset in zoomed pixels
    SheetLeft: int
    /// Y view topmost offset in zoomed pixels
    SheetTop: int
    /// Draw2D canvas element width in unzoomed pixels
    CanvasX: int
    /// Draw2D canvas element hight in unzoomed pixels
    CanvasY: int
    /// Zoom factor for scaling. > 1 => shrink objects, < 1 => magnify objects
    Zoom: float
}


let getViewableXY (sPos: ScrollPos) =
    let scale pos = int(float pos * sPos.Zoom)
    let lTop = scale sPos.SheetLeft, scale sPos.SheetTop
    {
        LTop = lTop
        RBot =  
            lTop |> 
            fun (x,y) -> x + scale sPos.SheetX, y + scale sPos.SheetY
    }

let checkOnCanvas sPos box =
    let {LTop=(x1,y1); RBot=(x2,y2)} = box
    let {LTop=(x1',y1'); RBot=(x2',y2')} = getViewableXY sPos
    x1 < x1' || y1 < y1' || x2 > x2' || y2 > y2'
    |> not

/// Obtain scroll and zoom values to put box into viewable area
/// If zoom is adjusted the highest magnification that allows this is used
/// zoom is limited by max and min values
/// too small zoomMax might mean the box is not viewable
/// Return Error on box not viewable or box not on canvas
let GetNewPos (zoomMin: float) (zoomMax: float) (model: Model) ( box: Bbox) (sPos: ScrollPos) =
    let x1,y1 = box.LTop
    let x2,y2 = box.RBot
    let zoomIdeal = max ((float x2-float x1)/(float sPos.SheetX)) ((float y2-float y1)/(float sPos.SheetY))
    let zoom = 
        if zoomIdeal < zoomMin then 
            zoomMin else 
        if zoomIdeal > zoomMax then
            zoomMax else
            zoomIdeal

    let scale pos = int (float pos / float sPos.Zoom)
    let newScrollPos =
        { sPos with
            Zoom = zoom
            SheetLeft = (scale x1) 
            SheetTop = (scale y1) 
        }
    if checkOnCanvas newScrollPos box then  
        Ok newScrollPos
    else
        Error <| sprintf "Can't view %A inside the allowed box of %A" box (getViewableXY newScrollPos)



type Direction = TOP | BOTTOM | LEFT | RIGHT | MID

let rTop bb = match bb with {LTop=(x,y); RBot=(x',y')} -> (x',y)
let lBot bb = match bb with {LTop=(x,y); RBot=(x',y')} -> (x,y')

let sheetDefault = {
    SheetX =1000
    SheetY = 1000
    SheetLeft = 0
    SheetTop = 0
    CanvasX = CommonTypes.draw2dCanvasWidth
    CanvasY = CommonTypes.draw2dCanvasHeight
    Zoom = 1.0
    }


/// get current Draw2D schematic sheet scroll and zoom position
/// component positions are always set or returned as unscaled, and with no offset
/// sheet values, offsets, and scaling change what portion of draw2d canvas is seen
let scrollData model =
    let scrollArea model  = model.Diagram.GetScrollArea()
    let zoomOpt model = model.Diagram.GetZoom()
    match scrollArea model, zoomOpt model with 
        | Some a, Some z -> 
            printfn "Area = (%d,%d,%d,%d, %.2f)" a.Width a.Height a.Left a.Top z
            let mag n = int (float n * z)
            let mag' n = min n (int (float n * z))
            
            {
                SheetX = mag' a.Width 
                SheetY = mag' a.Height
                SheetLeft = mag a.Left
                SheetTop = mag a.Top
                Zoom = z
                CanvasX = CommonTypes.draw2dCanvasWidth
                CanvasY = CommonTypes.draw2dCanvasHeight
            }
        | _ -> sheetDefault


let computeBoundingBox (boxes: Bbox list) =
    let bbMin = fst (List.minBy (fun xyPos -> fst xyPos.LTop) boxes).LTop , snd (List.minBy (fun xyPos -> snd xyPos.LTop) boxes).LTop
    let bbMax = fst (List.maxBy (fun xyPos -> fst xyPos.RBot) boxes).RBot , snd (List.maxBy (fun xyPos -> snd xyPos.RBot) boxes).RBot
    {LTop=bbMin; RBot=bbMax}

let computeVertexBBox (conn:Connection) =
    let verts = conn.Vertices
    if verts = [] then  failwithf "computeVertexBBox called with empty list of vertices!"
    let intFst = fst >> int
    let intSnd = snd >> int
    let bbMin = (List.maxBy (fun (x,y) -> x) verts |> intFst), (List.minBy (fun (x,y) -> y) verts |> intSnd)
    let bbMax = (List.maxBy (fun (x,y) -> x) verts |> intFst), (List.minBy (fun (x,y) -> y) verts |> intSnd)
    {LTop=bbMin; RBot=bbMax}


/// Choose a good position to place the next component on the sheet based on where existing
/// components are placed. One of 3 heuristics is chosen.
//  Return (x,y) coordinates as accepted by draw2d.
let getNewComponentPosition (model:Model) =

    let maxX = 60
    let maxY = 60
    let offsetY = 30
   
    let meshPitch1 = 45
    let meshPitch2 = 5

    let sDat = scrollData model

    let bbTopLeft = {LTop=(sDat.SheetLeft,sDat.SheetTop); RBot=(sDat.SheetLeft,sDat.SheetTop)}

    let isVisible (x,y) = x >= sDat.SheetLeft && y >= sDat.SheetTop && x < sDat.SheetLeft + sDat.SheetX - maxX && y < sDat.SheetTop + sDat.SheetY - maxY

    let componentPositions , boundingBox, comps  =
        match model.Diagram.GetCanvasState () with
        | None -> 
            printfn "No canvas detected!"
            [bbTopLeft],bbTopLeft, []
        | Some jsState ->
            let comps,conns = Extractor.extractState jsState
            let xyPosL =
                comps
                |> List.map (fun co -> {LTop=(co.X,co.Y); RBot=(co.X+co.W,co.Y+co.H)})
                |> List.filter (fun co -> isVisible co.LTop)
            if xyPosL = [] then 
                [bbTopLeft],bbTopLeft, [] // add default top left component to keep code from breaking
            else
                xyPosL, computeBoundingBox xyPosL, comps
    /// x value to choose for y offset heuristic
    let xDefault =
        componentPositions
        |> List.filter (fun bb -> isVisible bb.LTop)
        |> List.map (fun bb -> bb.LTop)
        |> List.minBy snd
        |> fst
        |> (fun x -> min x (sDat.SheetX - maxX))
        

    /// y value to choose for x offset heuristic
    let yDefault =
        componentPositions
        |> List.filter (fun bb -> isVisible bb.LTop)
        |> List.map (fun bb -> bb.LTop)
        |> List.minBy fst
        |> snd
        |> (fun y -> min y (sDat.SheetY - maxY))

    /// work out the minimum Euclidean distance between (x,y) and any existing component
    let checkDistance (compBb) =
        let {LTop=(xRef,yRef)} = compBb
        let dir (x,y) bb = 
            let d1 =
                match x < fst bb.LTop, x <= fst bb.RBot with
                | true, _ -> LEFT
                | _, false -> RIGHT
                | _ -> MID
         
            let d2 =
                match y < snd bb.LTop, y <= snd bb.RBot with
                | true, _ -> TOP
                | _, false -> BOTTOM
                | _ -> MID
            (d2,d1)
        
        let avg x x' = (float x + float x' ) / 2.

        let euclidean (pt:int*int) (bb:Bbox) = 
            let euc (x,y) (x',y') (x'',y'') = 
                let (xx,yy) = avg x' x'', avg y' y''
                sqrt(((float x - xx)**2. + (float y - yy)**2.)/2.)
            match dir pt bb with
            | TOP, LEFT -> euc pt bb.LTop bb.LTop
            | TOP, MID -> euc pt bb.LTop (rTop bb)
            | TOP, RIGHT -> euc pt (rTop bb) (rTop bb)
            | BOTTOM, LEFT -> euc pt (lBot bb) (lBot bb)
            | BOTTOM, MID -> euc pt (lBot bb) bb.RBot
            | BOTTOM, RIGHT -> euc pt bb.RBot bb.RBot
            | MID, LEFT -> euc pt bb.LTop (lBot bb)
            | MID, MID -> 0.
            | MID, RIGHT -> euc pt (rTop bb) bb.RBot
            | x -> failwithf "What? '%A' Can't happen based on definition of dir!" x
        
        let euclideanBox (bb:Bbox) (bb1:Bbox) =
            ((List.min [ euclidean bb.RBot bb1 ; euclidean bb.LTop bb1; euclidean (rTop bb) bb1; euclidean (lBot bb) bb1 ]), bb1)
            |> (fun (x, _) ->
                if x <> 0. then x else
                    let bbAv {LTop=(x,y);RBot=(x',y')} = avg x x', avg y y'
                    let (x,y) = bbAv bb
                    let (x',y')=bbAv bb1
                    -(float maxX) - (float maxY) + sqrt((x - x')**2. + (y - y')**2.))
        componentPositions
        |> List.filter (fun {LTop=(x,y)} -> abs(x-xRef) < 3*maxX && abs(y-yRef) < 3*maxY)
        |> List.map (euclideanBox compBb)
        |> (function |[] -> float (sDat.SheetX + sDat.SheetY) | lst -> List.min lst)

            

    let xyToBb (x,y) = {LTop=(x,y); RBot=(x+maxX,y+maxY)}

    /// get from model the correct draw2d coords of the last component added.
    let lastCompPos =
        match model.CreateComponent with
        | None -> None
        | Some cComp -> 
            match List.tryFind (fun (comp:Component) -> comp.Id = cComp.Id) comps with
            | Some comp -> Some (comp.X, comp.Y, comp.H, comp.W)
            | None -> None


    match boundingBox.RBot, lastCompPos with
    | _ when boundingBox = bbTopLeft -> 
        // Place first component on empty sheet top middle
        sDat.SheetLeft + sDat.SheetX / 2 - maxX / 2, sDat.SheetTop
    | _, Some (x,y,h,w) when checkDistance {LTop=(x,y+h+offsetY); RBot=(x+w,y+2*h+offsetY)} > float 0 && y + h + offsetY < sDat.SheetTop + sDat.SheetY - maxY -> 
        // if possible, place new component just below the last component placed, even if this has ben moved.
        x, y + h + offsetY
    | (_,y),_ when y < sDat.SheetY + sDat.SheetTop - 2*maxY && y > sDat.SheetTop -> 
        // if possible, align horizontally with vertical offset from lowest component
        // this case will ensure components are stacked vertically (which is usually wanted)
        xDefault, y + maxY
    | (x,_),_ when x < sDat.SheetX + sDat.SheetLeft - 2*maxX && x > sDat.SheetTop -> 
        // if possible, next choice is align vertically with horizontal offset from rightmost component
        // this case will stack component horizontally
        x + maxX, yDefault
    | _ ->
        // try to find some free space anywhere on the sheet
        // do a coarse search for largest Euclidean distance to any component's worst case bounding box
        List.allPairs [sDat.SheetLeft+maxX..meshPitch1..sDat.SheetLeft+sDat.SheetX-maxX] [sDat.SheetTop+maxY..meshPitch1..sDat.SheetTop+sDat.SheetY-maxY]
        |> List.map xyToBb
        |> List.sortByDescending (checkDistance)
        |> List.take 10
        |> List.collect (fun {LTop=(xEst,yEst)} ->
                //now do the same thing locally with a narrower search pitch
                List.allPairs [xEst - meshPitch1/3..meshPitch2..xEst + meshPitch1/3] [yEst - meshPitch1/3..meshPitch2..yEst + meshPitch1/3]
                |> List.filter isVisible // delete anything too near edge
                |> List.map xyToBb
                |> List.maxBy checkDistance
                |> (fun  bb -> [bb]))
        |> List.maxBy checkDistance
        |> (fun bb -> bb.LTop)
    |> (fun (x,y) -> printf "Pos=(%d,%d)" x y; (x,y))
        

 


    

        

let private menuItem label onClick =
    Menu.Item.li
        [ Menu.Item.IsActive false; Menu.Item.Props [ OnClick onClick ] ]
        [ str label ]

let private makeCustom model loadedComponent =
    menuItem loadedComponent.Name (fun _ ->
        let custom = Custom {
            Name = loadedComponent.Name
            InputLabels = loadedComponent.InputLabels
            OutputLabels = loadedComponent.OutputLabels
        }
        model.Diagram.CreateComponent custom loadedComponent.Name 100 100
        |> ignore
    )

let private makeCustomList model =
    match model.CurrProject with
    | None -> []
    | Some project ->
        // Do no show the open component in the catalogue.
        project.LoadedComponents
        |> List.filter (fun comp -> comp.Name <> project.OpenFileName)
        |> List.map (makeCustom model)

let private createComponent comp label model dispatch =
    let x,y = getNewComponentPosition model
    match model.Diagram.CreateComponent comp label x y with
    | Some jsComp -> 
        Extractor.extractComponent jsComp
        |> SetCreateComponent 
        |> dispatch
        |> ignore
    | None -> ()
    ReloadSelectedComponent model.LastUsedDialogWidth |> dispatch

let private createIOPopup hasInt typeStr compType (model:Model) dispatch =
    let title = sprintf "Add %s node" typeStr
    let beforeText =
        fun _ -> str <| sprintf "How do you want to name your %s?" typeStr
    let placeholder = "Component name"
    let beforeInt =
        fun _ -> str <| sprintf "How many bits should the %s node have?" typeStr
    let intDefault = model.LastUsedDialogWidth
    let body = 
        match hasInt with
        | true -> dialogPopupBodyTextAndInt beforeText placeholder beforeInt intDefault dispatch
        | false -> dialogPopupBodyOnlyText beforeText placeholder dispatch
    let buttonText = "Add"
    let buttonAction =
        fun (dialogData : PopupDialogData) ->
            let inputText = getText dialogData
            let inputInt = getInt dialogData
            createComponent (compType inputInt) (formatLabelFromType (compType inputInt) inputText) model dispatch
            if hasInt then dispatch (ReloadSelectedComponent inputInt)
            dispatch ClosePopup
    let isDisabled =
        fun (dialogData : PopupDialogData) ->
            (getInt dialogData < 1) || (getText dialogData = "")
    dialogPopup title body buttonText buttonAction isDisabled dispatch

let private createNbitsAdderPopup (model:Model) dispatch =
    let title = sprintf "Add N bits adder"
    let beforeInt =
        fun _ -> str "How many bits should each operand have?"
    let intDefault = model.LastUsedDialogWidth
    let body = dialogPopupBodyOnlyInt beforeInt intDefault dispatch
    let buttonText = "Add"
    let buttonAction =
        fun (dialogData : PopupDialogData) ->
            let inputInt = getInt dialogData
            printfn "creating adder %d" inputInt
            createComponent (NbitsAdder inputInt) "" {model with LastUsedDialogWidth = inputInt} dispatch
            dispatch ClosePopup
    let isDisabled =
        fun (dialogData : PopupDialogData) -> getInt dialogData < 1
    dialogPopup title body buttonText buttonAction isDisabled dispatch

let private createSplitWirePopup model dispatch =
    let title = sprintf "Add SplitWire node" 
    let beforeInt =
        fun _ -> str "How many bits should go to the top wire? The remaining bits will go to the bottom wire."
    let intDefault = 1
    let body = dialogPopupBodyOnlyInt beforeInt intDefault dispatch
    let buttonText = "Add"
    let buttonAction =
        fun (dialogData : PopupDialogData) ->
            let inputInt = getInt dialogData
            createComponent (SplitWire inputInt) "" model dispatch
            dispatch ClosePopup
    let isDisabled =
        fun (dialogData : PopupDialogData) -> getInt dialogData < 1
    dialogPopup title body buttonText buttonAction isDisabled dispatch

let private createRegisterPopup regType (model:Model) dispatch =
    let title = sprintf "Add Register" 
    let beforeInt =
        fun _ -> str "How wide should the register be (in bits)?"
    let intDefault = model.LastUsedDialogWidth
    let body = dialogPopupBodyOnlyInt beforeInt intDefault dispatch
    let buttonText = "Add"
    let buttonAction =
        fun (dialogData : PopupDialogData) ->
            let inputInt = getInt dialogData
            printfn "Reg inutInt=%d" inputInt
            createComponent (regType inputInt) "" model dispatch
            dispatch ClosePopup
    let isDisabled =
        fun (dialogData : PopupDialogData) -> getInt dialogData < 1
    dialogPopup title body buttonText buttonAction isDisabled dispatch

let private createMemoryPopup memType model dispatch =
    let title = "Create memory"
    let body = dialogPopupBodyMemorySetup model.LastUsedDialogWidth dispatch
    let buttonText = "Add"
    let buttonAction =
        fun (dialogData : PopupDialogData) ->
            let addressWidth, wordWidth = getMemorySetup dialogData
            let memory = {
                AddressWidth = addressWidth
                WordWidth = wordWidth
                Data = List.replicate (pow2 addressWidth) (int64 0) // Initialise with zeros.
            }
            createComponent (memType memory) "" model dispatch
            dispatch ClosePopup
    let isDisabled =
        fun (dialogData : PopupDialogData) ->
            let addressWidth, wordWidth = getMemorySetup dialogData
            addressWidth < 1 || wordWidth < 1
    dialogPopup title body buttonText buttonAction isDisabled dispatch

let private makeMenuGroup title menuList =
    details [Open true] [
        summary [menuLabelStyle] [ str title ]
        Menu.list [] menuList
    ]

let viewCatalogue model dispatch =
    Menu.menu [] [
            makeMenuGroup
                "Input / Output"
                [ menuItem "Input"  (fun _ -> createIOPopup true "input" Input model dispatch)
                  menuItem "Output" (fun _ -> createIOPopup true "output" Output model dispatch)
                  menuItem "Wire Label" (fun _ -> createIOPopup false "label" (fun _ -> IOLabel) model dispatch)]
            makeMenuGroup
                "Buses"
                [ menuItem "MergeWires"  (fun _ -> createComponent MergeWires "" model dispatch)
                  menuItem "SplitWire" (fun _ -> createSplitWirePopup model dispatch) ]
            makeMenuGroup
                "Gates"
                [ menuItem "Not"  (fun _ -> createComponent Not "" model dispatch)
                  menuItem "And"  (fun _ -> createComponent And "" model dispatch)
                  menuItem "Or"   (fun _ -> createComponent Or "" model dispatch)
                  menuItem "Xor"  (fun _ -> createComponent Xor "" model dispatch)
                  menuItem "Nand" (fun _ -> createComponent Nand "" model dispatch)
                  menuItem "Nor"  (fun _ -> createComponent Nor "" model dispatch)
                  menuItem "Xnor" (fun _ -> createComponent Xnor "" model dispatch) ]
            makeMenuGroup
                "Mux / Demux"
                [ menuItem "Mux2" (fun _ -> createComponent Mux2 "" model dispatch)
                  menuItem "Demux2" (fun _ -> createComponent Demux2 "" model dispatch) ]
            makeMenuGroup
                "Arithmetic"
                [ menuItem "N bits adder" (fun _ -> createNbitsAdderPopup model dispatch) ]
            makeMenuGroup
                "Flip Flops and Registers"
                [ menuItem "D-flip-flop" (fun _ -> createComponent DFF "" model dispatch)
                  menuItem "D-flip-flop with enable" (fun _ -> createComponent DFFE "" model dispatch)
                  menuItem "Register" (fun _ -> createRegisterPopup Register model dispatch)
                  menuItem "Register with enable" (fun _ -> createRegisterPopup RegisterE model dispatch) ]
            makeMenuGroup
                "Memories"
                [ menuItem "ROM (asynchronous)" (fun _ -> createMemoryPopup AsyncROM model dispatch)
                  menuItem "ROM (synchronous)" (fun _ -> createMemoryPopup ROM model dispatch)
                  menuItem "RAM" (fun _ -> createMemoryPopup RAM model dispatch) ]
            makeMenuGroup
                "This project"
                (makeCustomList model)
        ]